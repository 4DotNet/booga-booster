## Context

Guests exist in two modules today. The **Queue** module generates them (`PersonGenerator`, Bogus-driven, seeded through `QueueModuleOptions.RandomSeed`) as an immutable `Person` (number, name, weight) grouped into `QueuedGroup`s inside the `RideQueue` aggregate; the filler service enqueues them and `RideQueueService` exposes them as `PersonDto`/`QueuedGroupDto` through `IRideQueueService` (the module's `.Abstractions` contract). The **DigitalTwin** module boards them: `RideLoadingCoordinator` takes a group from the queue, reduces every member to a `PassengerWeight`, and `Ride.BoardGroup` seats `Passenger`s into randomly chosen empty `Gondola`s. A `Gondola` already computes the felt lateral/forward G every physics tick in `AdvancePhysics`, and the ride already has a maximum allowed G (`RideParameters.MaxGForce = 4.5`, `docs/05 §5.7`). `Ride.Advance(dt)` runs at a fixed 1/120 s and is deterministic: all randomness goes through `IPersonGenerator` / `IRideEventSampler`, time through `TimeProvider`.

The dashboard's right rail (`ride-dashboard.html`) stacks the Security, Speed and Gondola panels, all fed by `RideStateService` over the SSE stream; the Queue panel in the left rail is fed by `QueueStateService` over HTTP polling. PrimeNG 22 is installed and mandated for new UI but not used by any panel yet.

Constraints: rich domain models per ADR-0003 (validated value objects, intent-revealing mutation), feature slices per ADR-0005/6, no magic numbers in the physics (every constant in `RideParameters` with its rationale in `docs/`), determinism, at least 80 % line coverage on both module libraries, WCAG AA on the frontend, and no personal data on spans or metrics (ADR-0009).

## Goals / Non-Goals

**Goals:**
- Give every guest a validated rider profile (preferred intensity, happiness, nausea) that is generated on arrival, eroded by a long wait, carried onto the ride, and evolved deterministically by the ride's felt G-force.
- Expose the mood as aggregates (queue average happiness on the queue status; rider count, average happiness and average nausea on the telemetry snapshot) and show them in a new dashboard panel.
- Keep every tunable in one place (`QueueModuleOptions` for the queue, `RideParameters` plus `docs/06` for the ride) so the model can be tuned without hunting.

**Non-Goals:**
- Guests leaving the queue because they are unhappy (balking/reneging). Happiness is reported, not acted upon.
- Per-seat or per-gondola mood on the wire or in the UI; the requested panel is three averages. Per-seat values can be added later without changing this design.
- Scripted ride programmes that optimise for happiness. That is the `Controller` module's future job; this change only produces the signal.
- Publishing mood on the Dapr bus. `GroupQueuedIntegrationEvent` stays as it is.
- Modelling boredom: a ride *tamer* than a rider prefers leaves their happiness unchanged (see Open Questions).

## Decisions

### D1. One `RiderProfile` value object per module, not a shared domain type
Both modules get their own `RiderProfile` record (`Queue.Domain.RiderProfile`, `DigitalTwin.Domain.RiderProfile`) validating `PreferredIntensity` in `[0.1, 1]`, `Happiness` in `[0, 1]`, `Nausea` in `[0, 1]` and rejecting NaN/infinity, mirroring `PassengerWeight`. The two are bridged only by the plain numbers on `PersonDto` (Queue.Abstractions), which the DigitalTwin already consumes.
- *Alternative: a shared type in `Shared/Core`.* Rejected; `Core` must hold no module-specific logic (ADR-0004), and a rider's mood is domain, not infrastructure.
- *Alternative: three loose doubles on `Person`.* Rejected; ADR-0003 wants multi-value invariants in a validated value object, and the three values always travel together.

### D2. Profile generation lives in `PersonGenerator`, seeded like weight
`PersonGenerator.Next()` draws `PreferredIntensity` uniformly in `[0.1, 1]`, `Happiness` uniformly in `[0.65, 0.85]` and sets `Nausea = 0`, using the same Bogus `Randomizer` that already honours `RandomSeed`, so a seeded test reproduces the profiles exactly. The happiness bounds are constants next to the existing weight-distribution constants.
- *Alternative: a separate `IRiderProfileSampler`.* Rejected; `IPersonGenerator` is already the seam that produces "who arrives", and a second sampler would only split one seeded source into two.

### D3. Queue grumpiness is a pure function of the wait, not a background mutation
`RideQueue.EnqueueAll(arrivals, now)` stamps each new `QueuedGroup.QueuedAt` with the caller's `DateTimeOffset` (from `RideQueueService`'s injected `TimeProvider`). A `GrumpinessPolicy(Onset, RatePerMinute)` value object, built from two new options (`GrumpinessOnset` default 5 min, `GrumpinessRatePerMinute` default `0.01`) and held by the aggregate, computes `current = clamp(initial - rate * max(0, waited - onset) in minutes, 0, 1)`. `QueuedGroup.CurrentHappiness(member, now)` and `RideQueue.AverageHappiness(now)` (null when the line is empty) expose it. The stored `Person` never changes while waiting.
- *Alternative: a hosted service that decrements happiness every N seconds.* Rejected; it adds a second clock to the module, makes the reported value depend on scheduler timing, and the value is a closed-form function of the wait anyway.
- Consequence: `TakeGroupAsync` reports the wait-adjusted happiness at take time, which is exactly the value the rider boards with.

### D4. Wire contract: additive fields on Queue.Abstractions
`PersonDto` gains `PreferredIntensity`, `Happiness` (wait-adjusted at snapshot time) and `Nausea`. `GetQueueStatusResponse` gains `double? AverageHappiness` (null when nobody is waiting). `QueuedGroupDto` is unchanged apart from its people. `PersonDto` stays at the `DataTransferObjects` root because it is shared across features (as its remarks already state); the `dto-organization` skill governs the rest.

### D5. Ride intensity is the gondola's felt horizontal G relative to the maximum allowed G
`Gondola` computes, where it already computes `LateralG`/`ForwardG`, `HorizontalG = sqrt(LateralG^2 + ForwardG^2)`, `Intensity = clamp(HorizontalG / RideParameters.MaxGForce, 0, 1)` and `IsAtGLimit = HorizontalG >= MaxGForce`. Intensity therefore shares the rider preference's `[0, 1]` scale by construction.
- *Alternative: intensity from commanded power or mill rpm.* Rejected; riders feel G, not throttle, and two gondolas on the same ride feel very different G (docs/05 §5.3, the beat). The request explicitly says "g forces touch their limits".
- Horizontal only: gravity's constant 1 g would put the intensity floor at `1/4.5` on a stationary ride; the horizontal specific force is what the ride adds.

### D6. Mood dynamics run inside `Gondola.AdvancePhysics`, once per tick, on seated passengers
After `UpdateGForces`, the gondola calls `Experience(Intensity, dt)` on each seated passenger. `Passenger.Experience(intensity, dt)` applies, with `delta = intensity - PreferredIntensity` and `tol = RideParameters.IntensityMatchTolerance` (0.1):
- `|delta| <= tol`: `Happiness += HappinessGainPerSecond (0.1) * dt`
- `delta > tol`: `Happiness -= HappinessLossPerSecond (0.1) * dt` and `Nausea += NauseaGainPerSecond (0.2) * dt`
- `delta < -tol`: no change (non-goal: boredom)

Both values are clamped to `[0, 1]`. Because `AdvancePhysics` runs only while the ride is `Started`, `Stopping` or `EmergencyStop`, mood is frozen while loading, safe and offloading: a stationary rider at 1 g feels nothing.
- *Alternative: a separate `RiderExperience` domain service iterating all 32 seats from `Ride.Advance`.* Rejected; the gondola owns the G-force it computes, and putting the call next to `UpdateGForces` keeps intensity and its effect in one tick with no double bookkeeping.
- Determinism holds: the update is a pure function of state and `dt`, no randomness, no wall clock.

### D7. The sustained-G penalty is a gondola-level latch
`Gondola` accumulates `_secondsAtGLimit` while `IsAtGLimit`, resets it to zero the tick G drops below the limit, and when it first crosses `RideParameters.SustainedGLimitSeconds` (2 s) adds `SustainedGLimitNauseaPenalty` (0.5) to every seated passenger **once**; the latch re-arms only after G has dropped below the limit. Applies regardless of preference: two seconds at 4.5 g gets to everyone.
- *Alternative: per-passenger timers.* Rejected; the G is a property of the gondola, so the timer is too, and one counter per gondola is cheaper than two.

### D8. Passengers are constructed by the caller and boarded whole
`Passenger` becomes `Passenger(PassengerWeight, RiderProfile)` with `PreferredIntensity` immutable and `Happiness`/`Nausea` mutable only through `Experience` and `AddNausea`. `Ride.BoardGroup` and `IRideStore.BoardGroup` take `IReadOnlyList<Passenger>` instead of weights; `RideLoadingCoordinator` maps each `PersonDto` to a `Passenger` (weight plus profile from the DTO's wait-adjusted values). The manual `BoardPassenger` command boards a `Passenger` whose profile comes from a new `IRideEventSampler.NextRiderProfile()` (default: uniform preference, happiness in `[0.65, 0.85]`, nausea 0, the same distribution as arriving guests). `Passenger.OfWeight(kg)` stays for tests and uses a neutral profile (`DefaultPreferredIntensity = 0.5`, `DefaultHappiness = 0.75`, nausea 0) from `RideParameters`.

### D9. Telemetry roll-up is computed on the backend
`RideTelemetry` gains `RiderMoodTelemetry Riders` = `(int RiderCount, double? AverageHappiness, double? AverageNausea)`, nulls when nobody is aboard; `GreatMill` computes it over occupied seats in `ToTelemetry()`. `SeatTelemetry` is **not** extended (non-goal).
- *Alternative: per-seat values with the frontend averaging.* Rejected for now; it triples the seat payload at 30 Hz for a panel that shows three numbers. It remains the obvious extension if a per-gondola view is wanted.

### D10. Observability (ADR-0009): aggregates only, never a person
- `GetQueueStatusQueryHandler.EnrichActivityWithResponse` tags `queue.happiness.average`.
- `GetRideTelemetryQueryHandler.EnrichActivityWithResponse` tags `ride.riders.happiness.average` and `ride.riders.nausea.average`.
- On offload (the `GreatMill.Offload` to `Seat.Unboard` path), record each leaving rider's final happiness and nausea in two new untagged histograms on `BoogaBoosterTelemetry` (`ride.rider.happiness.final`, `ride.rider.nausea.final`). The distribution is what an operator wants to trend, and no identity or name is attached.
- Attribute names are constants in `QueueTelemetryAttributes` / `RideTelemetryAttributes` per the existing convention.

### D11. Frontend: one presentational `RiderMoodPanel`, fed by both state services
New `ride-dashboard/panels/rider-mood-panel/rider-mood-panel.ts` (`bb-rider-mood-panel`, OnPush, signal inputs `queueHappiness`, `riderHappiness`, `nausea`, each `number | null`, plus `queueUnavailable: boolean`). It renders three labelled meters using PrimeNG (consult the `primeng` MCP server for the current `MeterGroup`/`ProgressBar` API and a11y guidance before choosing) with the value also written as text (`72 %`) and explicit "No riders" / "Queue empty" / "Unavailable" text states, so colour never carries meaning alone. `ride.models.ts` adds `RiderMood` and maps `riders` from the stream DTO (defaulting to nulls for older frames); `queue.models.ts` maps `averageHappiness`. `RideStateService.riderMood` and `QueueStateService.averageHappiness` expose the signals; `ride-dashboard.html` adds a fourth `.panel` in the right rail after `bb-gondola-panel`. Fake/simulated sources gain the fields so existing specs stay green.

### D12. Documentation is part of the physics change
`docs/06-rider-experience.md` documents the intensity mapping (D5), the mood dynamics (D6, D7) and the queue grumpiness (D3) with the same "what this gives the test suite" table the other docs have; `docs/appendix-parameters.md` gains rows for every new constant; `docs/README.md` links the new document.

## Risks / Trade-offs

- **The tuning numbers are educated guesses** (`0.1/s`, `0.2/s`, `0.01/min`, tolerance `0.1`). Mitigation: all live in `RideParameters` / `QueueModuleOptions` with rationale in `docs/06`, so retuning is a one-line edit and no test hard-codes a literal.
- **A full-power ride may rarely reach 4.5 g horizontally**, making `IsAtGLimit` and the sustained penalty uncommon in normal operation. Mitigation: acceptable, the penalty is meant for abuse; the continuous intensity path (D6) does the everyday work. Tests drive G through a known kinematic state rather than waiting on the motor curve.
- **Queue happiness uses wall-clock `TimeProvider`, ride happiness uses simulation time.** Mitigation: the queue is already wall-clock driven (the filler), and the simulation runs in real time in production; in tests both are faked. Documented in `docs/06`.
- **Additive DTO changes.** Mitigation: old frames without `riders`/`averageHappiness` map to nulls and the panel shows its text placeholder; no consumer breaks.
- **Payload growth.** Three numbers per frame and three per person on the queue poll; negligible.

## Migration Plan

Additive, no persisted state (both aggregates are in-memory). Ship backend and frontend together so the panel has data; rollback is reverting the change. Existing tests that construct `Passenger`s or call `BoardGroup` with weights are updated in the same change.

## Open Questions

- **Tamer than preferred.** The request specifies the matching and the too-intense cases only. This design leaves happiness unchanged when the ride is tamer than the rider prefers. Confirm, or specify a boredom rate.
- **Grumpiness rate.** "Slowly decrease" is implemented as `0.01` happiness per minute beyond a five-minute wait (a 45-minute wait costs `0.4`). Confirm the default.
- **Nausea start value.** "Usually starts at 0" is implemented as always `0` on arrival. Confirm, or specify the exception.
