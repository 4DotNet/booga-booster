# Design

## Context

Guests live in two modules that share no domain types:

- **Queue.**
  - `Person` is an immutable number, name and weight, produced by the seeded `PersonGenerator`. The generator uses a Bogus `Faker` and honours `QueueModuleOptions.RandomSeed`.
  - `RideQueue.EnqueueAll` wraps each `GroupArrival` in a `QueuedGroup`.
  - `RideQueueService` projects groups to `QueuedGroupDto`/`PersonDto` for `GetStatus` and `TakeGroupAsync`. It already injects a `TimeProvider`, but the queue does not record *when* a group joined.
- **DigitalTwin.**
  - `RideLoadingCoordinator` maps each taken `PersonDto` to a bare `PassengerWeight` and calls `IRideStore.BoardGroup` → `Ride.BoardGroup`.
  - A `Passenger` wraps only a `PassengerWeight`. It is owned by the `Seat` it boards.
  - `Ride.Advance(dt)` steps the physics only in `Started`, `Stopping` and `EmergencyStop`. In that branch, `Gondola.AdvancePhysics` recomputes `_forwardG`/`_lateralG`, the horizontal specific force in g, via `UpdateGForces`.
  - `RideParameters.MaxGForce` is already 4.5 g (`docs/05 §5`).

The constraints come from CLAUDE.md:
- The tick is deterministic at a fixed 1/120 s step.
- Randomness goes through seeded samplers, and time goes through `TimeProvider`.
- Every physics constant lives in `RideParameters` and is derived in `docs/`.
- Modules talk only through `.Abstractions`.

The requirements are in `specs/`. The motivation is in `proposal.md`.

## Goals / Non-Goals

**Goals:**
- Mood is a deterministic function of state and time. It comes from seeded draws on arrival, a closed-form function of wait time in the queue, and fixed-step updates on the ride.
- Mood crosses the module boundary only on the existing `PersonDto` → boarding path. No new coupling, event or endpoint.
- Every tuning constant is named, derived in `docs/06-rider-experience.md` and pinned by a test.

**Non-Goals:**
- Mood has no effect on behaviour. Nobody leaves the queue, refuses to board or changes the arrival rate, and nausea does not lower happiness.
- Ride telemetry carries only the ride-wide summary, with no mood per seat or per guest.
- Mood is not tracked after a passenger leaves the ride. There is no history, no satisfaction event and nothing returned to the queue.
- Mood does not change which group boards or where its members sit.

## Decisions

### D1: Queue happiness is computed at read time from the wait, not stored and decremented

`Person` keeps its **arrival happiness** fixed. `QueuedGroup` gains `QueuedAt`. `RideQueueService` reads the time from its `TimeProvider` and passes it into `RideQueue.EnqueueAll`, so the domain never reads a clock. The current happiness at wait `w` is a pure function:

```
h(w) = h₀                                                   w ≤ P
h(w) = max(0, h₀ − A · (2^((w − P) / T) − 1))               w > P
P = 5 min (patience),  T = 5 min (loss-doubling time),  A = 5 pts
```

The loss is 5, 15, 35 and 75 points at 10, 15, 20 and 25 minutes. `Person.HappinessAfterWaiting(TimeSpan)` implements this. `RideQueueService.ToDto` evaluates it at `TimeProvider.GetUtcNow() − QueuedAt`, so status snapshots and `TakeGroupAsync` agree.

*Alternative: a hosted service that lowers happiness every second.* Rejected. It needs a timer loop, mutable state under the queue lock and timer-driven tests, all to approximate a closed-form curve.

*Alternative: exponential decay, `h₀ · 2^(−(w−P)/T½)`.* Rejected. Decay loses the most in the first minute after the threshold and then flattens out, which is the opposite of a crowd losing patience. "Decrease exponentially" is read as an accelerating loss, which also matches the nausea model (D6).

### D2: Arrival draws are whole numbers, made after name and weight

`PersonGenerator.Next()` draws happiness with `Random.Int(65, 85)` and then preferred intensity with `Random.Int(50, 100)`. Both draws come after the name and weight. Nausea is set to 0. The ranges are named constants on `Person`, next to `MinWeightInKilograms`. Because the draws come last, the first person's seeded name and weight do not change. Later people shift because the random stream has advanced, and any test that depends on them is updated in the same commit.

### D3: Mood crosses the boundary on `PersonDto`

`PersonDto` gains `double Happiness`, `int PreferredRideIntensity` and `double Nausea`. `GetQueueStatusResponse` gains `double? AverageHappiness`, which is `null` when the queue is empty. `TakeGroupAsync` already returns `PersonDto`s, so the coordinator gets each guest's mood as it was when they left the queue, without a new contract. `PersonDto` stays at the `DataTransferObjects` root because several features share it, as its remark explains. This satisfies the `dto-organization` skill. The other known DTO-placement discrepancies are out of scope.

### D4: A `PassengerProfile` value object replaces the bare weight when boarding

A new DigitalTwin value object, `PassengerProfile(PassengerWeight Weight, double Happiness, int PreferredRideIntensity, double Nausea)`, checks on construction that each score is within [0, 100]. ADR-0003 says a change to several values goes through one validated value object. The profile replaces `PassengerWeight` in `Ride.BoardGroup`, `IRideStore.BoardGroup` and the coordinator mapping.

`Passenger` is constructed from a profile. `Weight` and `PreferredRideIntensity` never change. `Happiness` and `Nausea` have private setters and change only through intent-revealing methods: `ExperienceIntensity(double intensity, double dt)` (D5, D6) and `SufferOverGShock()` (D7), each of which clamps to [0, 100]. The single-seat `BoardPassenger` feature has only a weight, so it uses `PassengerProfile.Neutral(weight)`, which sets happiness 75, preferred intensity 75 (the arrival-range midpoints, as `RideParameters` constants) and nausea 0. `Passenger.OfWeight` keeps working, so existing tests are unaffected.

### D5: Intensity is the total felt G divided by `G_max`

In each `Gondola`, right after `UpdateGForces` in `AdvancePhysics`:

```
G_felt    = √(ForwardG² + LateralG² + 1)            // horizontal specific force ⊕ 1 g gravity (docs/05 §5.1)
intensity = min(100, 100 · G_felt / G_max)           // G_max = RideParameters.MaxGForce = 4.5 g
```

This is the literal reading of "50 % of the max allowed G-forces" (2.25 g). At rest the intensity is 22, below every possible preference (≥ 50), so a parked ride pleases nobody. The gondola exposes `Intensity` internally for tests. It is not added to the per-gondola telemetry (D9).

*Alternative: `(G_felt − 1)/(G_max − 1)`, so rest reads 0.* Rejected. It moves "50 %" to 2.75 g, which contradicts the request.

### D6: Happiness gain uses a triangular comfort band, and nausea grows exponentially once the gap reaches 30 points

With `Δ = intensity − preferred` in percentage points, `Passenger.ExperienceIntensity` applies each step:

```
dH/dt = R_h · max(0, 1 − |Δ| / B)          R_h = 1 pt/s,  B = 15 pp
if Δ ≥ 30:  N ← (N + N₀) · e^{k·dt} − N₀   k = ln 2 / 10 s,  N₀ = 1
```

- **Happiness.** The gain is at its maximum for a perfect match and falls to zero 15 points either side. One well-matched minute adds about 60 points, which is "significantly happier" against a 65–85 start. A too-tame ride for a thrill-seeker adds nothing but does not take anything away either, because the request names no penalty.
- **Nausea.** The step is the exact solution of `dN/dt = k(N + N₀)` over `dt`, so the result does not depend on the step size. The offset `N₀` is needed because plain `dN/dt = kN` never leaves 0, and every guest boards with nausea 0. Starting from 0, nausea reaches 3 after 20 s, 15 after 40 s and 63 after 60 s, then caps at 100. The gap uses the *signed* Δ, so only a ride that is too intense causes nausea. Below the threshold, nausea holds steady. It does not recover during the ride.

### D7: The over-G shock fires once per sustained episode

`Gondola` keeps an `_overGSeconds` accumulator and an `_overGEpisodeCharged` flag. On each physics step:
- If `G_felt ≥ G_max`, add `dt` to the accumulator. The first time it passes 1 s, set the flag and call `SufferOverGShock()` on each occupant: +25, clamped.
- Otherwise, reset both.

This reads "every time this occurs" as *each time the condition arises*, not *every further second*. The per-second reading would pin every rider at 100 within 4 s of a sustained over-G, leaving the panel nothing to show.

### D8: Mood updates run only inside the existing physics branch

`Gondola.AdvancePhysics` is already called only from the `Started`/`Stopping`/`EmergencyStop` branch of `Ride.Advance`. The mood update (D5–D7) runs at the end of `AdvancePhysics`, through new `Seat.ExperienceIntensity` and `Seat.SufferOverGShock` methods that pass the call to the occupant, if there is one. No state check is needed, and loading and offloading cannot change mood. The step size is the twin's fixed `dt`, so mood is exactly as deterministic as the physics. The cost is about ten floating-point operations per gondola per tick at 120 Hz, which is negligible. Mood updates still run when a gondola's brake is engaged, because the G-forces are still computed.

### D9: The ride-wide summary is added to telemetry

`DigitalTwin.Abstractions` gains `RiderExperienceTelemetry(int RiderCount, double? AverageHappiness, double? AveragePreferredRideIntensity, double? AverageNausea)`, added to `RideTelemetry` as its last member, `RiderExperience`. `Ride.ToTelemetry()` builds it in one pass over the occupied seats, at most 32. Every average is `null` when `RiderCount` is 0. The summary goes out on the existing SSE stream and `GET` telemetry endpoint without any change to them. The values are sent as raw doubles, and the panel rounds them. The new DTOs follow the existing DigitalTwin.Abstractions telemetry-record layout. They are stream records, not feature request/response pairs.

*Alternative: send per-seat mood and compute the averages in the frontend.* Rejected. It adds three fields × 32 seats to every 30 Hz frame for no benefit, and it puts the averaging rule on both sides of the wire.

### D10: The panel is a presentational PrimeNG component fed by existing state

`ride-dashboard/panels/rider-experience-panel` (`bb-rider-experience-panel`) uses OnPush and takes four `input<number | null>()` signals. `ride-dashboard.html` places it in a new `.panel` directly after the gondola panel and binds it to two sources:
- `QueueStateService` supplies `averageHappiness` from the existing poll.
- `RideStateService` supplies a `riderExperience` computed signal from the SSE stream.

The panel has no services, which keeps it simple to test, like `bb-gondola-panel`. Each row is a PrimeNG `p-progressbar` with `showValue` turned off, a visible label and a rounded value. The accessible name comes from `aria-labelledby` pointing at the label, and the value text is "N out of 100". A `null` input shows a no-data message instead of the bar. The rows sit in a polite live region, like the other telemetry panels.

This is the app's first PrimeNG component, so `app.config.ts` gains `providePrimeNG` with a `@primeuix/themes` preset. The exact API is checked with the `primeng` MCP server before coding. The bar colours must meet WCAG AA contrast against the panel background in both themes.

## Risks / Trade-offs

- **Normal running almost never reaches `G_max`.** `docs/05 §5.3` puts the felt-G beat between about 1.0 and 2.8 g, so intensity tops out around 62. Riders who prefer 100 % rarely reach their peak, and the over-G shock does not fire in normal running. This follows the request literally, and the effect is intended: pushing the ride towards `G_max` pleases thrill-seekers and makes everyone else sick. → Domain tests drive gondola G directly. `docs/06` records the range that can be reached. Retuning the ride programme is a separate concern (`Controller` module, `passenger-safety`).
- **Interaction with `passenger-safety`.** Its over-speed trip makes over-G shocks even rarer. The two changes do not conflict in code: one checks rpm limits, the other accumulates G.
- **The extra random draws shift seeded sequences.** → D2: draw last, and update the affected expectations deliberately in the same commit.
- **The tuning constants are judgement calls.** P, T, A, R_h, B, k and N₀ are reasoned defaults, not measured data. → Each one is named in `RideParameters` (ride side) or on `Person` (queue side), with its reasoning in `docs/06`, so retuning is a one-line change backed by a test.
- **The queue does not store current happiness.** A future persistent queue would have to store `QueuedAt` and the arrival happiness, not the reduced value. → This is documented on `Person`.
- **Signature change inside DigitalTwin.** `BoardGroup` changes from taking `PassengerWeight` to taking `PassengerProfile`. → It is internal to the module, not part of `.Abstractions`. The callers and tests change in the same commit.

## Migration Plan

Every wire change is additive: new DTO and telemetry fields and a new panel. There is no data to migrate. The frontend maps a missing `averageHappiness` or `riderExperience` to `null`, so during a mixed deploy the panel shows its no-data states instead of failing. To roll back, revert the branch.
