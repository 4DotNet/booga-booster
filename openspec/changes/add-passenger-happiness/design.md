# Design

## Context

Guests exist in two modules that never share domain types:

- **Queue**: `Person` holds a number, a name and a weight. It is immutable and produced by the seeded `PersonGenerator`. A `QueuedGroup` lives in a `RideQueue` until `IRideQueueService.TakeGroupAsync` removes it and returns `QueuedGroupDto`/`PersonDto`. Today the queue does not record *when* a group joined.
- **DigitalTwin**: `RideLoadingCoordinator` maps each `PersonDto` to a bare `PassengerWeight` and calls `Ride.BoardGroup`. A `Passenger` is only a weight, owned by a `Seat`. The ride advances in fixed 1/120 s steps (`Ride.Advance`). Physics only runs in `Started`, `Stopping` and `EmergencyStop`, and each `Gondola` updates its `ForwardG`/`LateralG` (horizontal specific force, in g) on every step.

Hard constraints: the tick is deterministic (CLAUDE.md), randomness goes through seeded samplers, time goes through `TimeProvider`, and every physics/model constant lives in `RideParameters` with its derivation in `docs/`. Modules talk only through `.Abstractions`.

## Goals / Non-Goals

**Goals:**
- Mood is a deterministic function of state and time: seeded draws on arrival, pure functions of wait time in the queue, and fixed-step updates on the ride.
- Mood crosses the module boundary only through the existing `PersonDto` → boarding path, with no new coupling.
- Every tuning constant is named, documented and testable.

**Non-Goals:**
- Nausea does not feed back into happiness, and happiness does not change behaviour. Guests do not leave the queue, refuse to board, or affect arrival rates.
- Mood is not recovered or tracked after a rider leaves the ride on offload. There is no "ride satisfaction" event or history.
- No integration events carry mood.
- No per-seat mood in telemetry; only the ride-wide summary.
- Preferences do not influence which group boards or where it sits.

## Decisions

### D1: Queue happiness is computed from wait time, not ticked

A `Person` keeps its **arrival happiness** fixed. Its happiness at a given wait `w` is a pure function:

```
h(w) = h₀                                       w ≤ 5 min
h(w) = h₀ · 2^(−(w − 5 min) / T½)               w > 5 min,   T½ = 10 min
```

`QueuedGroup` records `QueuedAt` (taken from `TimeProvider` at enqueue, passed into `RideQueue.EnqueueAll`). `RideQueueService` evaluates `h` against `TimeProvider.GetUtcNow()` when it builds a DTO, so status snapshots and boarding see the same value.

*Alternative: a background service that decrements happiness every second.* Rejected. It adds a hosted loop, mutable shared state under the queue lock, and timer-dependent tests, all to approximate a closed-form curve. The read-time function is exact and trivially testable with `FakeTimeProvider`.

The 5-minute threshold and the 10-minute half-life are constants on the queue's domain model (alongside `Person.MinWeightInKilograms`), documented in `docs/06-rider-experience.md`.

### D2: Mood crosses the boundary on `PersonDto`

`PersonDto` gains `Happiness` (evaluated at snapshot time), `PreferredRideIntensity` and `Nausea`. `GetQueueStatusResponse` gains `AverageHappiness` (`double?`, `null` when nobody waits). Because `TakeGroupAsync` already returns `PersonDto`s, the coordinator receives the mood the guest has at the moment they leave the line. No new contract or event is needed. `PersonDto` stays where it is: it is shared by several features, as its existing remark explains, so this follows the `dto-organization` rule.

### D3: A passenger profile replaces the bare weight at boarding

A new `PassengerProfile` value object in the DigitalTwin domain holds weight, happiness, preferred intensity and nausea. Each 0–100 score is validated on construction (ADR-0003: multi-value changes go through a validated value object). It replaces `IReadOnlyList<PassengerWeight>` in `Ride.BoardGroup`. `Passenger` is built from it: `Weight` and `PreferredRideIntensity` are fixed, while `Happiness` and `Nausea` are private-set and change only through intent-revealing methods (`ExperienceRide(intensity, dt)`, `SufferOverGShock()`), clamped to [0, 100].

The single-passenger `BoardPassenger` feature only receives a weight. It uses a **default profile**, `RideParameters.DefaultPassengerHappiness` = 75 and `DefaultPreferredRideIntensity` = 75 (the midpoints of the arrival ranges), with nausea 0. `Passenger.OfWeight` keeps working this way, so existing tests are unaffected.

### D4: Intensity is total felt G over `G_max`

Per gondola, on every physics step:

```
G_felt    = √(ForwardG² + LateralG² + 1)          // horizontal specific force ⊕ 1 g of gravity (doc 5)
intensity = min(100, 100 · G_felt / G_max)        // G_max = RideParameters.MaxGForce = 4.5 g
```

This follows the request literally ("50 % of the max allowed G-forces" means 2.25 g). At rest the intensity is 22 %, below every possible preference (≥ 50), so a parked ride pleases nobody.

*Alternative: map `(G − 1)/(G_max − 1)` so rest = 0 %.* Rejected. It shifts the "50 %" point to 2.75 g, which contradicts the stated requirement.

### D5: Happiness gain is a triangular comfort band

With `Δ = intensity − preferred` (percentage points):

```
dH/dt = R_h · max(0, 1 − |Δ| / B)        R_h = 1.0 pt/s,  B = 15 pp
```

The gain is full when the ride matches the rider's preference and fades to zero 15 points either side. Outside the band, happiness is unchanged. A 50 %-rider therefore peaks around 2.25 g and a 100 %-rider at 4.5 g. At 1 pt/s a well-matched minute is worth about 60 points, which is "significantly happier" against a 65–85 start.

### D6: Nausea grows exponentially past a 30-point over-intensity

When `Δ ≥ 30`, nausea grows as

```
dN/dt = k · (N + N₀)      k = ln 2 / 10 s,  N₀ = 1
```

It is integrated exactly per step: `N ← (N + N₀)·e^{k·dt} − N₀`. The `N₀` seed is needed because a pure `dN/dt = kN` never leaves 0, and every guest boards at 0. From 0, nausea passes ~3 after 20 s, ~15 after 40 s and ~63 after 60 s: a slow start, then an unmistakable climb. It is clamped at 100. Below the threshold nausea holds steady and does not recover on the ride.

### D7: The over-G shock fires once per sustained episode

Each `Gondola` accumulates time while `G_felt ≥ G_max` and resets the accumulator as soon as it drops below. When the accumulator first passes **1 s**, every occupant gets `+25` nausea (clamped), and the episode is marked as charged. The next shock needs the G to drop below `G_max` and climb back for another full second. This reads "every time this occurs" as *each time the condition arises*, not *each further second*. That avoids a ride parked at `G_max` pinning everyone at 100 within 4 s.

### D8: Mood updates only while the physics runs

`Ride.Advance` drives the mood updates (D5–D7) inside the existing physics branch (`Started`, `Stopping`, `EmergencyStop`), right after the gondola's G-forces are updated. Loading and offloading leave mood untouched. Everything is in-step and fixed-`dt`, so the model inherits the twin's determinism.

### D9: A ride-wide summary on telemetry

`RideTelemetry` gains `RiderExperience: RiderExperienceTelemetry(int RiderCount, double? AverageHappiness, double? AveragePreferredRideIntensity, double? AverageNausea)`. The averages are `null` when nobody is aboard, which covers every seated passenger in any state. It is computed in `Ride.ToTelemetry()` (30 Hz, 32 seats at most, so the cost is negligible). It flows through the existing SSE stream and the `GET` telemetry endpoint unchanged.

### D10: The panel is a dumb PrimeNG component fed by existing state

`bb-rider-experience-panel` takes four `input<number | null>()` signals. `ride-dashboard.html` binds them from `QueueStateService` (queue average, from the existing poll) and `RideStateService` (rider summary, from SSE). Each metric renders a PrimeNG `p-progressbar` (consult the `primeng` MCP server first) with a visible label and a rounded value. Each bar has an accessible name and value text, and a `null` metric shows "No guests" instead of a bar at 0, so "no data" is never confused with "0 %". It is the first PrimeNG component in the app, so the theme provider must be checked in `app.config.ts`.

## Risks / Trade-offs

- **Extra random draws shift seeded sequences.** `PersonGenerator` draws two more values per person, so seeded tests that assert specific names or weights after the first person may change. → Draw mood *after* name and weight and update any affected expectations deliberately in the same commit.
- **`G_max` may rarely be reached.** If the default programme never approaches 4.5 g, 100 %-riders never peak and the over-G shock never fires in practice. → Domain tests drive the gondola G directly, and the doc records the reachable range. Tuning ride programmes is a separate concern (`Controller` module, `passenger-safety`).
- **Interaction with `passenger-safety`.** That change trips an over-speed stop. Over-G shocks may become rarer once it lands, which is the intended outcome, and the two changes do not conflict in code (rpm limits vs. G accumulator).
- **Tuning constants are judgement calls.** The 10-minute half-life, 15 pp band, 1 pt/s rate and 10 s nausea doubling are reasoned defaults, not measured data. → All are named `RideParameters`/queue constants with rationale in the docs, so retuning is a one-line, test-backed change.
- **Read-time decay means happiness is not stored.** A future persistent queue must store `QueuedAt` and arrival happiness, not the decayed value. → Documented on `Person`.

## Migration Plan

Purely additive: new DTO and telemetry fields, a new panel. There is nothing to migrate. Rolling back means reverting the branch, and the frontend tolerates the fields' absence during a mixed deploy because it treats a missing summary as `null`.
