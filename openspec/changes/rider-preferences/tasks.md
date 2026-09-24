## 1. Queue module — domain

- [ ] 1.1 Add `Domain/RiderProfile.cs` value object (`PreferredIntensity` in `[0.1, 1]`, `Happiness` and `Nausea` in `[0, 1]`, NaN/infinity rejected with `DomainValidationException`; range bounds as public constants)
- [ ] 1.2 Extend `Domain/Person.cs` with a required `RiderProfile Profile` constructor argument and property (public get, private set)
- [ ] 1.3 Add `Domain/GrumpinessPolicy.cs` value object (`Onset`, `RatePerMinute`, validated non-negative) with a pure `CurrentHappiness(double initial, TimeSpan waited)` clamped to `[0, 1]`
- [ ] 1.4 Extend `Domain/QueuedGroup.cs` with `QueuedAt`, `WaitedFor(DateTimeOffset now)`, `CurrentHappiness(Person member, DateTimeOffset now)` and `AverageHappiness(DateTimeOffset now)` using the policy it is constructed with
- [ ] 1.5 Extend `Domain/RideQueue.cs`: take a `GrumpinessPolicy` in the constructor, stamp `QueuedAt` in `EnqueueAll(arrivals, DateTimeOffset now)` (and `Enqueue`), and add `double? AverageHappiness(DateTimeOffset now)` (null when empty)

## 2. Queue module — generation, options, contract

- [ ] 2.1 Add `GrumpinessOnset` (default 5 min) and `GrumpinessRatePerMinute` (default 0.01) to `QueueModuleOptions.cs`, and build the policy where the store creates a `RideQueue`
- [ ] 2.2 In `Filling/PersonGenerator.cs`, draw `PreferredIntensity` uniformly in `[0.1, 1]` and `Happiness` uniformly in `[0.65, 0.85]` from the seeded `Randomizer`, `Nausea = 0`; bounds as named constants beside the weight constants
- [x] 2.3 Extend `Abstractions/DataTransferObjects/PersonDto.cs` with `PreferredIntensity`, `Happiness`, `Nausea` and `GetQueueStatus/GetQueueStatusResponse.cs` with `double? AverageHappiness`
- [ ] 2.4 In `RideQueueService.cs`, pass `_timeProvider.GetUtcNow()` into `EnqueueAll`, map wait-adjusted happiness in `ToDto(group, member, now)` for `GetStatus` and `TakeGroupAsync`, and fill `AverageHappiness`
- [ ] 2.5 Add `queue.happiness.average` to `Observability/QueueTelemetryAttributes.cs` and tag it in `GetQueueStatusQueryHandler.EnrichActivityWithResponse`

## 3. Queue module — tests (xUnit v3, Moq, Bogus, no FluentAssertions)

- [ ] 3.1 `RiderProfile` validation: accepted values, each bound rejected, NaN/infinity rejected
- [ ] 3.2 `GrumpinessPolicy`: unchanged before and exactly at onset, linear decrease past onset (0.8 → 0.7 after 15 min at 0.01/min), floor at 0
- [ ] 3.3 `RideQueue`/`QueuedGroup`: `QueuedAt` stamped on enqueue, `CurrentHappiness` under a `FakeTimeProvider`, `AverageHappiness` mean and null-when-empty, stored `Person.Profile.Happiness` untouched
- [ ] 3.4 `PersonGenerator`: 1000 profiles within the arrival ranges, nausea always 0, identical sequences for the same seed
- [ ] 3.5 `RideQueueService`: `GetStatus` and `TakeGroupAsync` return wait-adjusted happiness and the response average; existing tests updated for the new `Person` constructor
- [ ] 3.6 `GetQueueStatusQueryHandler`: span carries `queue.happiness.average` and no per-person attribute
- [ ] 3.7 Run the Queue coverage report and confirm the module stays at or above 80 % line coverage (`test-coverage` skill)

## 4. DigitalTwin module — parameters and domain

- [ ] 4.1 Add to `Domain/RideParameters.cs`: `IntensityMatchTolerance = 0.1`, `HappinessGainPerSecond = 0.1`, `HappinessLossPerSecond = 0.1`, `NauseaGainPerSecond = 0.2`, `SustainedGLimitSeconds = 2`, `SustainedGLimitNauseaPenalty = 0.5`, `DefaultPreferredIntensity = 0.5`, `DefaultHappiness = 0.75`, each with an XML remark pointing at `docs/06`
- [ ] 4.2 Add `Domain/RiderProfile.cs` value object (same ranges and validation as the Queue one, referencing `RideParameters` for defaults)
- [ ] 4.3 Extend `Domain/Passenger.cs`: constructor `(PassengerWeight, RiderProfile)`, immutable `PreferredIntensity`, `Happiness`/`Nausea` with private setters, `Experience(double intensity, double dt)` implementing D6, `AddNausea(double amount)` clamped; keep `OfWeight(kg)` using the neutral default profile
- [ ] 4.4 Extend `Domain/Seat.cs` with an `Occupant` accessor (or an `Experience` pass-through) so the gondola can drive its seated passenger, and expose the leaving passenger's final happiness/nausea from `Unboard` for the offload histograms
- [ ] 4.5 Extend `Domain/Gondola.cs`: compute `HorizontalG`, `Intensity` and `IsAtGLimit` in `UpdateGForces`; after it, call `Experience(Intensity, dt)` on both seated passengers; add the `_secondsAtGLimit` latch that applies `SustainedGLimitNauseaPenalty` once per episode (D7); reset the latch when G drops below the limit
- [ ] 4.6 Change `Ride.BoardGroup` to accept `IReadOnlyList<Passenger>`; add a `RiderMood` roll-up (count, average happiness, average nausea over occupied seats) on `GreatMill` and project it in `Ride.ToTelemetry()`

## 5. DigitalTwin module — application, contract, observability

- [ ] 5.1 Add `Abstractions/RiderMoodTelemetry.cs` (`RiderCount`, `double? AverageHappiness`, `double? AverageNausea`) and a `Riders` parameter on `Abstractions/RideTelemetry.cs`
- [ ] 5.2 Add `NextRiderProfile()` to `Application/IRideEventSampler.cs` and implement it in `RandomRideEventSampler` (uniform preference in `[0.1, 1]`, happiness in `[0.65, 0.85]`, nausea 0)
- [ ] 5.3 Change `IRideStore.BoardGroup` / `RideStore.BoardGroup` to take `IReadOnlyList<Passenger>`; make `RideStore.BoardPassenger` build the passenger from the sampled weight and `NextRiderProfile()`
- [ ] 5.4 In `Application/RideLoadingCoordinator.cs`, map each taken `PersonDto` to `new Passenger(new PassengerWeight(kg), new RiderProfile(preferred, happiness, nausea))`
- [ ] 5.5 Add `ride.riders.happiness.average` and `ride.riders.nausea.average` to `Observability/RideTelemetryAttributes.cs` and tag them in `GetRideTelemetryQueryHandler.EnrichActivityWithResponse`
- [ ] 5.6 Add two untagged histograms `ride.rider.happiness.final` and `ride.rider.nausea.final` to `Shared/Core/Observability/BoogaBoosterTelemetry.cs` and record them for every passenger leaving during offload

## 6. DigitalTwin module — tests

- [ ] 6.1 `RiderProfile` (DigitalTwin) validation and `Passenger` construction; `OfWeight` uses the neutral profile
- [ ] 6.2 `Passenger.Experience`: matched gain (0.7 → 0.9 over 2 s at intensity 0.6), symmetric tolerance, cap at 1, too-intense loss and nausea gain (0.8 → 0.6 and 0 → 0.4 over 2 s at intensity 1 with preference 0.2), just-outside-tolerance case, nausea cap, tamer-ride no-op
- [ ] 6.3 `Gondola` intensity: 0 at rest, `2.25 g → 0.5`, saturation and `IsAtGLimit` at `5 g`, driven through a known kinematic state (mill/hub angle and omega inputs to `AdvancePhysics`)
- [ ] 6.4 `Gondola` sustained-G latch: penalty after 2 s, once per 5 s episode, no penalty for 1 s + 1 s excursions, re-arm after dropping below the limit, applies to a preference-1 rider
- [ ] 6.5 `Ride.Advance`: mood frozen while `Loading`/`Safe`/`Offloading`; changes while `Started`
- [ ] 6.6 `RideLoadingCoordinator`: boarded passenger carries the DTO's preferred intensity, happiness and nausea (Moq'd `IRideQueueService`)
- [ ] 6.7 Telemetry roll-up: averages over seated passengers, count 0 and null averages when empty, roll-up tracks changes over successive frames
- [ ] 6.8 Observability: `GetRideTelemetryQueryHandler` span attributes; offload records one histogram measurement per leaving passenger with no tags
- [ ] 6.9 Update every existing test that constructs `Passenger`s or calls `BoardGroup` with weights; run the DigitalTwin coverage report and confirm at or above 80 %

## 7. Docs

- [ ] 7.1 Write `docs/06-rider-experience.md`: intensity mapping (D5), mood dynamics (D6), sustained-G penalty (D7), queue grumpiness (D3), the wall-clock vs simulation-time note, and a "what this doc gives the test suite" table
- [ ] 7.2 Add rows for every new `RideParameters` constant and both Queue options to `docs/appendix-parameters.md`; link the new document from `docs/README.md`

## 8. Frontend — models and state

- [x] 8.1 In `ride-dashboard/models/ride.models.ts` add `RiderMood { riderCount; averageHappiness: number | null; averageNausea: number | null }`, the `riders` field on `RideTelemetryStreamDto` and `RideTelemetry`, and map it in `mapRideTelemetry` (nulls when absent)
- [x] 8.2 In `queue/models/queue.models.ts` add `averageHappiness: number | null` to `QueueStatusDto` and `QueueStatus` and map it in `toQueueStatus`
- [x] 8.3 Expose `riderMood` on `RideStateService` and `averageHappiness` on `QueueStateService`
- [x] 8.4 Update `testing/fake-ride-telemetry-source.ts`, `data/simulated-ride-telemetry-source.ts`, `sse-ride-telemetry-source.ts` (initial frame) and `queue/testing/fake-queue-source.ts` for the new fields

## 9. Frontend — Rider mood panel

- [x] 9.1 Consult the `primeng` MCP server for the `MeterGroup`/`ProgressBar` API, import path and accessibility guidance, and pick the component for the three meters
- [x] 9.2 Create `ride-dashboard/panels/rider-mood-panel/rider-mood-panel.ts` (`bb-rider-mood-panel`, OnPush, signal inputs `queueHappiness`, `riderHappiness`, `nausea`, `queueUnavailable`) rendering three labelled PrimeNG meters with the whole-number percentage as text and the "Queue empty" / "No riders" / "Unavailable" text states
- [x] 9.3 Add the panel to `ride-dashboard.html` as the last `.panel` of the right rail, after `bb-gondola-panel`, wired to `queue.averageHappiness()`, `queue.status()`, and `ride.riderMood()`

## 10. Frontend — tests (Vitest + axe-core)

- [x] 10.1 `ride.models.spec`: `riders` mapping including absent-field default; `queue.models.spec`: `averageHappiness` mapping
- [x] 10.2 `rider-mood-panel.spec.ts`: percentages (`0.72 → 72 %`, `0.6 → 60 %`, `0.25 → 25 %`), live update on input change, "Queue empty", "No riders" and "Unavailable" states, accessible names include metric name and value
- [x] 10.3 `rider-mood-panel.a11y.spec.ts`: axe-core WCAG AA audit with values and with each empty state
- [x] 10.4 `ride-dashboard.spec.ts`: the Rider mood panel is the last right-rail panel after the Gondolas panel
- [ ] 10.5 Run `npm test`, `npm run build` and `dotnet test BoogaBooster.slnx`; all green
