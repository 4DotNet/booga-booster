## Context

See `proposal.md` for the motivation. The design starts from the current state:

- **Queue**
  - `Person` (Queue domain) is immutable. It holds only `Number`, `Name` and `WeightInKilograms`.
  - `PersonGenerator` draws from a Bogus `Randomizer`, which can be seeded through `QueueModuleOptions.RandomSeed`.
  - `QueuedGroup` and `RideQueue` do not record when a group joined. `TimeProvider` is injected into `RideQueueService`, but it is used only to stamp `GroupQueuedIntegrationEvent`.
- **Boarding**
  - `RideLoadingCoordinator` (DigitalTwin) calls the synchronous `IRideQueueService.TakeGroupAsync` from Queue.Abstractions.
  - It maps each person to a `PassengerWeight` and nothing else. A seated `Passenger` is just a weight.
- **Physics**
  - The tick runs at a fixed 1/120 s (`RideParameters.TimeStep`) and is deterministic.
  - Each `Gondola` computes `ForwardG` and `LateralG` every tick in `UpdateGForces`.
  - `RideParameters.MaxGForce` is 4.5 g and is not used anywhere yet.
  - `docs/appendix-parameters.md` gives about 2 g at terminal speed, and the worked example peaks at about 2.8 g (`docs/05` §5.3).
- **Offloading** throws passengers away (`Seat.Unboard`). No experience data has to survive the ride.
- **Frontend**
  - The telemetry rail in `ride-dashboard.html` stacks the security, speed and gondola panels.
  - Panels are hand-rolled, OnPush and signal-driven.
  - Queue data comes from `QueueStateService`, which polls `GET /rides/{id}/queue`.
  - Ride data comes from `RideStateService`, which reads the SSE stream.

## Goals / Non-Goals

**Goals:**
- Experience dynamics are deterministic. They are pure functions of state, elapsed simulation time and seeded random draws.
- No magic numbers. Every tuning constant is named and its rationale is documented.
- All wire changes are additive. Existing clients keep working.

**Non-Goals:**
- Nausea does not feed back into happiness. Nausea does not decay or recover. Unhappy guests do not leave the queue (balking). There is no per-passenger display. All of these are possible follow-ups.
- No over-G protection or power reduction. Over-G only affects nausea here. Interlocks belong to the `passenger-safety` line of work.
- Experience is not persisted after offload.

## Decisions

### D1. Each module models experience in its own domain

The Queue `Person` gains `Happiness`, `PreferredRideIntensity` and `Nausea` (all `double`, validated to [0, 100]).

The DigitalTwin `Passenger` gains a `RiderExperience` value object with the same three values and intent-revealing mutators: `RaiseHappiness`, `AddNausea` and so on. Mutators clamp to [0, 100] rather than throw, because they are driven by continuous dynamics. Construction validates the range and throws `DomainValidationException`.

- *Alternative:* a shared `Rating` type in `Shared/Core`. Rejected because Shared must not hold module-specific logic, and the modules must not share domain types (ADR-0004). The duplicated range check is tiny.

### D2. Queue happiness decay is computed lazily from the enqueue time

`QueuedGroup` gets `QueuedAt` (`DateTimeOffset`), stamped from `TimeProvider` when the group is enqueued. `RideQueue.EnqueueAll` receives the timestamp; the domain never reads the clock.

A member's current happiness is a pure function of the starting value `H₀` and the wait time `w`:

```
H(w) = H₀                                  for w ≤ grace (5 min)
H(w) = H₀ · 2^(−(w − grace) / halfLife)    for w > grace
```

This is exponential decay with a configurable half-life (default 10 min). The function is evaluated when the status is read and when a group is taken for boarding. At take time the decayed value becomes the person's carried-over happiness.

`QueueModuleOptions` gains `HappinessGracePeriod` (default 5 min) and `HappinessHalfLife` (default 10 min).

- *Alternative:* a background ticker that mutates every person. Rejected because it adds a loop, locking and non-determinism relative to reads. The lazy form is exact at any instant and trivially testable with `FakeTimeProvider`.
- The user asked for happiness that "decreases exponentially". That is read as exponential decay towards 0. Reading it as a loss that grows exponentially would hit 0 abruptly and is less natural.

### D3. Starting values come from the existing seeded generator

`PersonGenerator` draws happiness as `Uniform[65, 85]` and preference as `Uniform[50, 100]` from the same `_faker.Random`. Nausea starts at 0.

The bounds live as named constants on the generator, or in `QueueModuleOptions` if they need to be tunable. Adding draws changes the random sequence for a given seed, so tests that pin seeded weights may need updating.

### D4. The hand-off goes through `PersonDto`

`PersonDto` gains `Happiness`, `PreferredRideIntensity` and `Nausea`. `TakeGroupAsync` returns members with their decayed happiness. `RideLoadingCoordinator` maps each `PersonDto` to a new `PassengerBoarding` record (weight + experience), and `Ride.BoardGroup` and `RideStore.BoardGroup` take it instead of a bare `PassengerWeight`.

- *Alternative:* an integration event. Rejected because boarding is already a synchronous Abstractions call, and an event would let the experience race the seat assignment.

### D5. Ride intensity is felt G as a share of max G

Felt G for a gondola is the magnitude of its forward and lateral components, `G = √(ForwardG² + LateralG²)`. These are the same values that are streamed.

```
intensity = clamp(100 · G / RideParameters.MaxGForce, 0, 100)
```

This follows the request literally: 50 % means 2.25 g and 100 % means 4.5 g.

- *Alternative:* normalise from 1 g (rest) to max G. Rejected because the request ties preference directly to "% of max allowed G-forces".

### D6. On-ride dynamics run inside the physics tick (dt = 1/120 s)

After `UpdateGForces`, `Gondola.AdvancePhysics` calls `seat.AdvanceExperience(intensity, dt)` for every occupied seat. Let `Δ = intensity − preference`.

- **Happiness gain.** If `|Δ| ≤ HappinessMatchBand` (15 points):
  ```
  H += HappinessGainRate · (1 − |Δ| / HappinessMatchBand) · dt
  ```
  `HappinessGainRate` is 2 pts/s. A perfect match takes a 75-happiness rider to 100 in about 12.5 s. Outside the band, happiness is unchanged.
- **Exponential nausea.** If `Δ ≥ NauseaIntensityExcess` (30 points):
  ```
  N += NauseaGrowthRate · (N + NauseaSeed) · dt
  ```
  `NauseaGrowthRate` is 0.15 /s and `NauseaSeed` is 1. This is the discrete form of `N(t) = (N₀ + 1)·e^{0.15t} − 1`, so nausea goes from 0 to 100 in about 31 s of sustained over-intensity. The seed lets growth start from 0.
- **Over-G episode.** Each gondola keeps an `_overGSeconds` accumulator and an `_overGEpisodeTriggered` flag.
  - While `G ≥ MaxGForce`, the accumulator adds `dt`.
  - When it first exceeds `OverGNauseaDwell` (1 s), every passenger gains `OverGNauseaPenalty` (25 points) and the flag is set.
  - When `G < MaxGForce`, both reset. One episode gives one penalty, however long it lasts.
  - "Gain 25 % nausea" is read as +25 points on the 0–100 scale. A multiplicative 25 % of 0 would do nothing.

All constants go in `RideParameters` under a new `// --- Passenger experience ---` section, with their derivation in `docs/06-passenger-experience.md`. They are also listed in `docs/appendix-parameters.md`.

- *Alternative:* update experience at the 30 Hz telemetry rate. Rejected because it ties domain behaviour to the presentation rate and breaks the "tick is the only clock" rule.

### D7. The telemetry summary is precomputed on the backend

`RideTelemetry` gains `RiderExperience: RiderExperienceTelemetry(int RiderCount, double? AverageHappiness, double? AveragePreferredIntensity, double? AverageNausea)`. The averages are `null` when nobody is on board.

The averages are computed in `Ride.ToTelemetry()` from the seats. Per-seat experience is not streamed: it would add 96 numbers per frame at 30 Hz that no UI needs.

`GetQueueStatusResponse` gains `double? AverageHappiness`, which is `null` for an empty queue.

### D8. The panel is a new PrimeNG-based component

`panels/rider-experience-panel/rider-experience-panel.ts`:

- It is OnPush with signal inputs (`queueHappiness`, `riderHappiness`, `riderIntensity`, `riderNausea`, each `number | null`).
- It renders four labelled PrimeNG `p-progressbar`s, the mandated component library (check with the `primeng` MCP server). Each bar shows its rounded value with a `%` unit and has an accessible name.
- A `null` value renders an explicit "No riders" or "Queue empty" state instead of a 0 % bar, so "nobody" is never confused with "very unhappy".
- The panel is a `<section aria-labelledby>` with an `<h2>`, inside a polite live region, like its siblings.

It is placed after `bb-gondola-panel` in the right rail. `RideStateService` exposes `riderExperience`, and `QueueStateService` exposes `averageHappiness`.

## Risks / Trade-offs

- **[Max G is rarely reached.]** At about 2–2.8 g typical peaks, intensity tops out near 45–62 %. High-preference riders will seldom hit their match band, and over-G episodes will be rare. This follows the request as written. Mitigation: the scale is a single constant (`MaxGForce`) and the behaviour is documented. Re-basing the scale, for example to a "comfortable max G", would be a follow-up spec change.
- **[The random sequence shifts.]** Extra draws per person change seeded outputs. Mitigation: update any seed-pinned tests in the same commit as the generator change.
- **[Overlap with `passenger-safety`.]** Both changes edit `RideTelemetry` and the `Ride` advance path. Mitigation: all edits are additive and in separate members. Whichever change merges second resolves the conflict.
- **[Per-tick cost.]** Up to 32 passengers × 120 Hz of scalar arithmetic is negligible. There are no allocations in the tick path: the value object is mutated through its owning `Passenger`, not reallocated.

## Migration Plan

All contract changes are additive. There is no data store and no rollout ordering. Rollback is reverting the PR.
