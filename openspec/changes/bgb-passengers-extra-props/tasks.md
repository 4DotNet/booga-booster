# Tasks

## 1. Queue domain: experience attributes and enqueue time

- [x] 1.1 Add `Happiness`, `PreferredRideIntensity` and `Nausea` to the Queue `Person` (constructor-validated 0–100, `DomainValidationException` when out of range) per `csharp-domain-model`. Verify with `Queue.Tests` covering valid, boundary and out-of-range values.
- [x] 1.2 Add `QueuedAt` to `QueuedGroup`. Pass the timestamp into `RideQueue.EnqueueAll` from `RideQueueService` using its `TimeProvider`. Verify with tests that use `FakeTimeProvider` and assert the recorded enqueue time.
- [ ] 1.3 Add `HappinessGracePeriod` (5 min) and `HappinessHalfLife` (10 min) to `QueueModuleOptions`. Implement the decay function `H(w)` from design D2 as a pure domain method. Verify with tests at 4, 6, 10, 15 and 25 minutes (80 → 40 at 15 min), including strict monotonic decrease after the grace period.
- [x] 1.4 Draw happiness `Uniform[65,85]` and preference `Uniform[50,100]` in `PersonGenerator` from the seeded `_faker.Random`, with nausea 0 and the bounds as named constants. Verify with a range test over ≥ 1 000 people and a same-seed reproducibility test. Update any seed-pinned tests.

## 2. Queue contracts and status

- [ ] 2.1 Extend `PersonDto` with `Happiness`, `PreferredRideIntensity` and `Nausea`. Map the *decayed* happiness at read/take time (status and `TakeGroupAsync`). Verify with tests showing a group taken after 15 minutes carries happiness 40 when it started at 80.
- [ ] 2.2 Add `double? AverageHappiness` to `GetQueueStatusResponse`, computed in the query handler (`null` when the queue is empty). Tag the activity via `EnrichActivityWithResponse` per `csharp-observability`. Verify with handler tests for 60/70/80 → 70 and for an empty queue → `null`.
- [ ] 2.3 Run `dotnet test` for `Queue.Tests` and check the `test-coverage` floor for the Queue module (≥ 80 % line coverage).

## 3. DigitalTwin domain: rider experience

- [ ] 3.1 Add the passenger-experience constants to `RideParameters` (`HappinessMatchBand` 15, `HappinessGainRate` 2, `NauseaIntensityExcess` 30, `NauseaGrowthRate` 0.15, `NauseaSeed` 1, `OverGNauseaDwell` 1 s, `OverGNauseaPenalty` 25). Write `docs/06-passenger-experience.md` with the formulas and derivations (design D5/D6), add the rows to `docs/appendix-parameters.md`, and link the new doc from `docs/README.md`. Verify that the doc renders and that every constant is referenced in it.
- [ ] 3.2 Add the `RiderExperience` value object (validated construction, clamping mutators) and attach it to `Passenger`. Verify with `DigitalTwin.Tests` for construction validation and clamping at 0 and 100.
- [ ] 3.3 Implement gondola ride intensity (`clamp(100·√(F²+L²)/MaxGForce, 0, 100)`). Verify with tests at 2.25 g → 50, 4.5 g → 100 and 5.0 g → 100.
- [ ] 3.4 Implement the per-tick happiness gain and exponential nausea growth via `Seat.AdvanceExperience`, called from `Gondola.AdvancePhysics`. Verify with tests: preference 50 at intensity 50 and preference 100 at intensity 100 both gain significantly over 5 s; an exact match beats a 10-point offset; preference 100 at intensity 40 shows no gain; preference 50 at intensity 85 shows growing per-interval nausea; preference 50 at intensity 75 shows no nausea.
- [ ] 3.5 Implement the per-gondola over-G episode timer (+25 nausea once per episode lasting more than 1 s). Verify with tests: 3 s gives +25, 0.5 s gives +0, and two separated episodes give +50.
- [ ] 3.6 Add a domain metric counter for over-G nausea episodes through `BoogaBoosterTelemetry.Meter`. Verify with a `MeterListener`-based test.
- [ ] 3.7 Add a determinism test: two identically seeded simulations produce identical passenger happiness and nausea after N ticks.

## 4. Boarding hand-off

- [ ] 4.1 Introduce the `PassengerBoarding` (weight + experience) input. Change `Ride.BoardGroup` and `RideStore.BoardGroup` to accept it, and map the full `PersonDto` in `RideLoadingCoordinator`. Verify with coordinator tests showing a boarded passenger has the queue's decayed happiness, preference and nausea, and confirm the existing boarding and loading tests still pass.

## 5. Telemetry contract

- [ ] 5.1 Add the `RiderExperienceTelemetry(RiderCount, AverageHappiness?, AveragePreferredIntensity?, AverageNausea?)` record to DigitalTwin.Abstractions and the `RiderExperience` property on `RideTelemetry`, populated in `Ride.ToTelemetry()`. Verify with tests for the spec example (70/90, 50/100, 0/20 → 80/75/10) and for an empty ride → `null` averages.
- [ ] 5.2 Run `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx`, and check the `test-coverage` floor for the DigitalTwin module.

## 6. Frontend: models and state

- [x] 6.1 Map the `riderExperience` telemetry field in `models/ride.models.ts`, and expose a `riderExperience` computed signal on `RideStateService`. Verify with Vitest model and service specs, including the `null` case.
- [x] 6.2 Map `averageHappiness` in `queue/models/queue.models.ts`, and expose an `averageHappiness` signal on `QueueStateService`. Verify with Vitest specs, including an empty queue.

## 7. Frontend: Rider Experience panel

- [x] 7.1 Consult the `primeng` MCP server for `ProgressBar` (API, a11y, theming). Build `panels/rider-experience-panel/rider-experience-panel.ts` (OnPush, signal inputs, four labelled `p-progressbar`s with `%` values, explicit "No riders" and "Queue empty" states). Verify with a Vitest spec covering values, live updates and empty states.
- [x] 7.2 Place `<bb-rider-experience-panel>` directly after `bb-gondola-panel` in `ride-dashboard.html`, wired to `RideStateService` and `QueueStateService`. Verify with a dashboard spec asserting the panel order in the right rail.
- [x] 7.3 Add `rider-experience-panel.a11y.spec.ts` (axe-core, with data and empty states, accessible names including "%"). Verify with `npx vitest run` on the spec and then `npm test` for the whole app.

## 8. Integration check

- [ ] 8.1 Run the AppHost (`dotnet run --project Aspire/FourDotnet.BoogaBooster.Aspire.AppHost`), load and run the ride, and confirm the Rider Experience panel shows queue happiness dropping for long waits and rider values updating while running.
