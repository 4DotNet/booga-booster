# Design

## Context

Guests exist in two modules that share no domain types:

- **Queue.** `Person` is an immutable number, name and weight, produced by the seeded `PersonGenerator` (Bogus `Faker`, honouring `QueueModuleOptions.RandomSeed`). `RideQueue.EnqueueAll` wraps each `GroupArrival` in a `QueuedGroup`. `RideQueueService` projects groups to `QueuedGroupDto`/`PersonDto` for `GetStatus` and `TakeGroupAsync`. It already has a `TimeProvider`, but the queue does not record *when* a group joined.
- **DigitalTwin.** `RideLoadingCoordinator` maps each taken `PersonDto` to a bare `PassengerWeight` and calls `IRideStore.BoardGroup` → `Ride.BoardGroup`. A `Passenger` wraps only a `PassengerWeight`, and the `Seat` that boards it owns it. `Ride.Advance(dt)` runs the physics only in `Started`, `Stopping` and `EmergencyStop`. Inside that, each `Gondola.AdvancePhysics` recomputes `ForwardG`/`LateralG`, the horizontal specific force in g.

Constraints from CLAUDE.md: the tick is deterministic at a fixed 1/120 s, randomness goes through seeded samplers, time goes through `TimeProvider`, every physics constant lives in `RideParameters` and is derived in `docs/`, and modules talk only through `.Abstractions`. The requirements are in `specs/`. The motivation is in `proposal.md`.

## Goals / Non-Goals

**Goals:**
- Mood is a deterministic function of state and time: seeded draws on arrival, a closed-form function of wait time in the queue, and fixed-step updates on the ride.
- Mood crosses the module boundary only on the existing `PersonDto` → boarding path, with no new coupling, event or endpoint.
- Every tuning constant is named, documented in `docs/06-rider-experience.md` and pinned by a test.

**Non-Goals:**
- Mood has no effect on behaviour. Nobody leaves the queue, refuses to board or changes the arrival rate. Nausea does not lower happiness.
- No per-seat or per-guest mood in ride telemetry, only the ride-wide summary.
- No mood after a passenger leaves the ride: no history, no satisfaction event, nothing returned to the queue.
- Mood does not affect which group boards or where its members sit.

## Decisions

### D1: Queue happiness is a read-time function of wait time, not a ticking value

`Person` keeps its **arrival happiness** fixed. `QueuedGroup` gains `QueuedAt`. `RideQueueService` reads the time from `TimeProvider` and passes it into `RideQueue.EnqueueAll`, so the domain never reads a clock. The current happiness at wait `w` is a pure function:

```
h(w) = h₀                                                   w ≤ P
h(w) = max(0, h₀ − A · (2^((w − P) / T) − 1))               w > P
P = 5 min (patience),  T = 5 min (loss-doubling time),  A = 5 pts
```

The loss at 10, 15, 20 and 25 minutes is 5, 15, 35 and 75 points. A guest who arrived at 75 reaches 0 at 25 minutes. `RideQueueService.ToDto` evaluates `h` at `TimeProvider.GetUtcNow()`, so status snapshots and `TakeGroupAsync` agree.

*Alternative: a hosted service that lowers happiness every second.* Rejected. It needs a timer loop, mutable state under the queue lock and timer-based tests, all to approximate a closed-form curve.

*Alternative: exponential decay, `h₀ · 2^(−(w−P)/T½)`.* Rejected. Decay loses the most points in the first minute after the threshold and then levels off, which is the opposite of a crowd running out of patience. The request says "decrease exponentially", and the accelerating form matches that and matches the nausea model (D6).

### D2: Arrival draws are whole numbers, made after name and weight

`PersonGenerator.Next()` draws happiness with `Random.Int(65, 85)` and preferred intensity with `Random.Int(50, 100)`, after the name and weight, and sets nausea to 0. The ranges are named constants on `Person`, next to `MinWeightInKilograms`. Making these draws last keeps every existing seeded name and weight sequence unchanged for the *first* person. Later people shift because the stream advances, and any test that depends on them is updated in the same commit.

### D3: Mood crosses the boundary on `PersonDto`

`PersonDto` gains `double Happiness`, `int PreferredRideIntensity` and `double Nausea`. `GetQueueStatusResponse` gains `double? AverageHappiness`, which is `null` when the queue is empty. `TakeGroupAsync` already returns `PersonDto`s, so the coordinator receives the guest's mood as it is when they leave the line, and no new contract is needed. `PersonDto` stays at the `DataTransferObjects` root because several features share it, as its remark says. That satisfies `dto-organization`, and no other DTO is moved by this change.

### D4: A `PassengerProfile` value object replaces the bare weight when boarding

A new DigitalTwin value object, `PassengerProfile(PassengerWeight Weight, double Happiness, int PreferredRideIntensity, double Nausea)`, checks on construction that each score is within [0, 100]. Under ADR-0003, a change to several values goes through one validated value object. It replaces `PassengerWeight` in `Ride.BoardGroup`, `IRideStore.BoardGroup` and the coordinator mapping.

`Passenger` is built from a profile. `Weight` and `PreferredRideIntensity` never change. `Happiness` and `Nausea` are private-set and change only through intent-revealing methods: `ExperienceIntensity(double intensity, double dt)` (D5, D6) and `SufferOverGShock()` (D7), each clamping to [0, 100]. The single-seat `BoardPassenger` feature only has a weight, so it uses `PassengerProfile.Default(weight)`: happiness 75 and preferred intensity 75 (the midpoints of the arrival ranges, as `RideParameters` constants) and nausea 0. `Passenger.OfWeight` keeps that behaviour, so existing tests are unaffected.

### D5: Intensity is total felt G divided by `G_max`

Each `Gondola`, right after `UpdateGForces` in `AdvancePhysics`:

```
G_felt    = √(ForwardG² + LateralG² + 1)            // horizontal specific force ⊕ 1 g gravity (docs/05 §5.1)
intensity = min(100, 100 · G_felt / G_max)           // G_max = RideParameters.MaxGForce = 4.5 g
```

This is the literal reading of "50 % of the max allowed G-forces" (2.25 g). At rest the intensity is 22, which is below every possible preference (≥ 50), so a parked ride pleases nobody.

*Alternative: `(G_felt − 1)/(G_max − 1)`, so rest reads 0.* Rejected. It moves "50 %" to 2.75 g, which contradicts the request.

### D6: Happiness uses a triangular comfort band; nausea grows exponentially once over-intensity reaches 30 points

With `Δ = intensity − preferred` (percentage points), applied per step in `Passenger.ExperienceIntensity`:

```
dH/dt = R_h · max(0, 1 − |Δ| / B)          R_h = 1 pt/s,  B = 15 pp
if Δ ≥ 30:  N ← (N + N₀) · e^{k·dt} − N₀   k = ln 2 / 10 s,  N₀ = 1
```

- **Happiness.** The gain is full at a perfect match and fades to zero 15 points either side. One well-matched minute adds about 60 points, which counts as "significantly happier" against a 65–85 start. Too little intensity for a thrill-seeker gives no gain, but it does not cost happiness either (non-goal: no penalties beyond the request).
- **Nausea.** The step is the exact solution of `dN/dt = k(N + N₀)` over `dt`, so its accuracy does not depend on the step size. The `N₀` seed is needed because plain `dN/dt = kN` never leaves 0, and every guest boards at 0. From 0 the values are 3 at 20 s, 15 at 40 s and 63 at 60 s, then the cap of 100. The threshold uses the *signed* Δ: only a ride that is too intense makes people sick. Below the threshold nausea holds steady and does not recover on the ride.

### D7: The over-G shock fires once per sustained episode

`Gondola` keeps an `_overGSeconds` accumulator and an `_overGEpisodeCharged` flag. On each physics step, if `G_felt ≥ G_max` it adds `dt`, and the first time the accumulator passes 1 s it calls `SufferOverGShock()` on each occupant (+25, clamped) and sets the flag. When `G_felt < G_max`, both reset. This reads "every time this occurs" as *each time the condition arises*, not *each further second*. The per-second reading would pin every rider at 100 within 4 s of a parked over-G ride, leaving nothing for the dashboard to show.

### D8: Mood updates inside the existing physics branch only

`Gondola.AdvancePhysics` already runs only from the `Started`/`Stopping`/`EmergencyStop` branch of `Ride.Advance`. The mood update (D5–D7) goes at the end of `AdvancePhysics`, through a new `Seat.ExperienceIntensity`/`Seat.SufferOverGShock` pass-through to the occupant. No state check is needed, and loading and offloading cannot change mood. The step size is the twin's fixed `dt`, so the model is as deterministic as the twin. It adds about ten floating-point operations per gondola per tick at 120 Hz, which is negligible.

### D9: A ride-wide summary on telemetry

`DigitalTwin.Abstractions` gains `RiderExperienceTelemetry(int RiderCount, double? AverageHappiness, double? AveragePreferredRideIntensity, double? AverageNausea)`, appended to `RideTelemetry` as `RiderExperience`. `Ride.ToTelemetry()` builds it in one pass over the occupied seats, at most 32. The averages are `null` when `RiderCount` is 0. It flows through the existing SSE stream and the `GET` telemetry endpoint unchanged. Raw doubles are sent, and the panel does the rounding.

*Alternative: compute the averages in the frontend from per-seat mood.* Rejected. The seat telemetry would grow by three fields × 32 seats on every 30 Hz frame for no gain, and the averaging rule would be duplicated across the wire.

### D10: The panel is a presentational PrimeNG component fed by existing state

`ride-dashboard/panels/rider-experience-panel` (`bb-rider-experience-panel`) is OnPush and takes four `input<number | null>()` signals. `ride-dashboard.html` binds them from `QueueStateService` (queue average, from the existing poll) and `RideStateService` (rider summary, from SSE). This keeps the panel free of services and easy to test, like `bb-gondola-panel`. Each row uses a PrimeNG `p-progressbar` with `showValue` off, plus a visible label and rounded value. The accessible name comes from `aria-labelledby` on the label, and the value text is "N out of 100". `null` renders the no-data message instead of the bar. The rows sit in a polite live region, as the existing telemetry panels do. This is the app's first PrimeNG component, so `app.config.ts` gains `providePrimeNG` with a `@primeuix/themes` preset. The `primeng` MCP server is consulted for the exact API before coding.

## Risks / Trade-offs

- **`G_max` is almost never reached with today's tuning.** `docs/05 §5.3` puts the felt-G beat between about 1.0 and 2.8 g. Intensity therefore tops out around 62: 100 %-riders rarely peak, and the over-G shock does not fire in normal running. This is the literal requirement, and it is the intended feedback: an operator who pushes the ride towards `G_max` pleases thrill-seekers and sickens everyone else. → Domain tests drive gondola G directly. The doc records the reachable range. Retuning the ride programme is a separate concern (`Controller` module, `passenger-safety`).
- **Interaction with `passenger-safety`.** That change trips an over-speed stop, which will make over-G shocks rarer still. The two do not conflict in code (rpm limits vs. the G accumulator).
- **Extra random draws shift seeded sequences.** → D2: draw last, and update affected expectations deliberately in the same commit.
- **Tuning constants are judgement calls.** P, T, A, R_h, B, k and N₀ are reasoned defaults, not measured data. → Each is named in `RideParameters` or on `Person`, with its rationale in `docs/06`, so retuning is a one-line, test-backed change.
- **Happiness is not stored in the queue.** A future persistent queue must store `QueuedAt` and the arrival happiness, not the declined value. → Documented on `Person`.
- **Breaking signature change inside DigitalTwin.** `BoardGroup` changes from `PassengerWeight` to `PassengerProfile`. → It is internal to the module (not in `.Abstractions`). The callers and tests are updated in the same commit.

## Migration Plan

All changes are additive on the wire: new DTO and telemetry fields and a new panel. There is no data to migrate. The frontend maps a missing `averageHappiness` or `riderExperience` to `null`, so a mixed deploy shows the no-data states instead of failing. Rollback is reverting the branch.
