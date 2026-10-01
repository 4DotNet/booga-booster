# Tasks

## 1. Specification docs and constants

- [x] 1.1 Write `docs/06-passenger-experience.md` deriving queue erosion (`H₀·e^(−(w−5 min)/τ_q)`), experienced intensity (horizontal felt G over `G_max`), the Gaussian happiness gain, the exact-step nausea growth (`dN/dt = r_n + λ_n·N`) and the max-G episode rule; link it from `docs/README.md` and verify every symbol in design D1 appears with a value and rationale
- [x] 1.2 Add the experience constants to `docs/appendix-parameters.md` (with a sanity check: 75 → 100 happiness at a perfect match ≈ 17 s; nausea from 0 at excess ≥ 30 reaches 100 in ≈ 18.5 s) and verify the table matches design D1
- [x] 1.3 Add `HappinessGainRate`, `HappinessMatchWidth`, `NauseaExcessThreshold`, `NauseaBaseRate`, `NauseaGrowthRate`, `MaxGEpisodeDuration`, `MaxGNauseaPenalty`, `MinExperienceRating`, `MaxExperienceRating` to `DigitalTwin/Domain/RideParameters.cs` with XML docs referencing `docs/06`; verify `dotnet build BoogaBooster.slnx` succeeds

## 2. Queue domain: ratings, join time and erosion

- [x] 2.1 Consult the `4dotnet-csharp-style-guide` MCP server (domain model, unit testing) and extend `Queue/Domain/Person.cs` with validated `Happiness`, `PreferredIntensity`, `Nausea` (0–100, `DomainValidationException` outside); verify with new `PersonTests` covering bounds and rejection
- [x] 2.2 Add the `QueuePatience` domain service (`GracePeriod` 5 min, `DecayTimeConstant` 10 min, `HappinessAfter(arrival, waited)`); verify with tests for 4:59 unchanged, the equal-ratio exponential property at 5/8/11 min, and clamping at 0
- [x] 2.3 Give `QueuedGroup` a `QueuedAt` and thread it through `RideQueue.Enqueue`/`EnqueueAll`; verify `RideQueueTests` assert each group keeps its own join time
- [x] 2.4 Extend `PersonGenerator` to draw happiness uniformly in [65, 85], preferred intensity in [50, 100] and nausea 0 via the seeded faker; verify with a 1,000-person range test and a same-seed reproducibility test

## 3. Queue contract: status and hand-off

- [x] 3.1 Run the `dto-organization` skill, then add `Happiness`, `PreferredIntensity`, `Nausea` to `PersonDto` and `double? AverageHappiness` to `GetQueueStatusResponse`, fixing any placement it flags; verify the solution builds
- [x] 3.2 In `RideQueueService`, stamp `QueuedAt` from `TimeProvider` on enqueue and map waited happiness in `GetStatus` (per person + average, null when empty) and `TakeGroupAsync`; verify with `FakeTimeProvider` tests that advance 10 min and assert eroded per-person values, the average, and an empty-queue null
- [x] 3.3 Add the average happiness as a tag in `GetQueueStatusQueryHandler.EnrichActivityWithResponse`; verify the handler test asserts the tag
- [x] 3.4 Run `dotnet test` for the Queue tests and the `test-coverage` skill; verify Queue module line coverage stays ≥ 80 %

## 4. DigitalTwin domain: riders and their evolution

- [x] 4.1 Consult the style-guide MCP server, then add the `PassengerExperience` value object (validated, clamped `With…`, pure `AfterRideStep(intensity, dtSeconds)` and `WithMaxGPenalty()`); verify unit tests for the Gaussian gain (50/50 beats 50/80; 100/100 beats 100/50), no-gain-loss, the nausea threshold at 29 vs 30 excess, the closed-form exact-step nausea value and the clamp at 100
- [x] 4.2 Add `BoardingPassenger`, give `Passenger` an `Experience` with intent methods `ExperienceRideStep` and `SufferSustainedMaxG` (calling `MarkChanged`), and change `Ride.BoardGroup`/`IRideStore.BoardGroup`/`RideStore` to take `IReadOnlyList<BoardingPassenger>`; verify existing boarding tests pass after updating their construction sites and a new test asserts the boarded rider carries its experience
- [x] 4.3 In `Gondola.AdvancePhysics`, compute experienced intensity (`min(100, 100·|G|/MaxGForce)`), apply `ExperienceRideStep` to occupants, and track the max-G episode (accumulate while ≥ `MaxGForce`, penalise once after > 1 s, reset below); verify `GondolaTests` for 2.25 g → 50, 5 g → 100, 0.8 s → no penalty, 3 s → one penalty, two episodes → two penalties, empty seat untouched
- [x] 4.4 Verify with a `Ride` test that riders' ratings do not change while Loading or Offloading and do change while Started

## 5. DigitalTwin contract: telemetry and loading hand-off

- [x] 5.1 Add `RiderExperienceTelemetry(double? AverageHappiness, double? AveragePreferredIntensity, double? AverageNausea)` to DigitalTwin.Abstractions and as a member of `RideTelemetry`; compute it in `Ride.ToTelemetry()`; verify tests for the 60/90, 50/100, 0/50 → 75/75/25 example and the all-null empty ride
- [x] 5.2 Map `PersonDto` → `BoardingPassenger` in `RideLoadingCoordinator`; verify a coordinator test with a mocked `IRideQueueService` asserts the boarded riders' happiness equals the DTO's (waited) happiness
- [x] 5.3 Verify the SSE endpoint serialises the new field (camelCase `riderExperience`) with an endpoint/serialisation test, and run the `test-coverage` skill to confirm DigitalTwin coverage stays ≥ 80 %

## 6. Frontend: Rider Experience panel

- [x] 6.1 Consult the `primeng` MCP server (setup, `progressbar`, accessibility, theming) and wire `providePrimeNG` with the Aura preset in `app.config.ts`, scoped by CSS layer so existing panels are not restyled; verify `npm run build` succeeds and the existing specs still pass
- [x] 6.2 Add `RiderExperience` to `ride.models.ts`, map `riderExperience` in `mapRideTelemetry` (default all-null when absent) and expose `riderExperience` from `RideStateService`; verify mapping specs for present, empty and missing fields
- [x] 6.3 Add `averageHappiness` to `queue.models.ts`, its mapping and `QueueStateService`, plus the fake source; verify the queue state spec covers a value and null
- [x] 6.4 Build `panels/rider-experience-panel` (OnPush, signal inputs, four labelled `p-progressbar` rows with visible rounded numeric text, "No data" state, `aria-labelledby`, polite live region); verify a component spec for values, "No data", and live updates
- [x] 6.5 Add a `rider-experience-panel.a11y.spec.ts` axe audit for the with-data and no-data states; verify zero WCAG AA violations
- [x] 6.6 Place the panel in `ride-dashboard.html` directly after the gondola panel and bind queue and ride state in `ride-dashboard.ts`; verify a dashboard spec asserts it follows the gondola panel in the telemetry rail

## 7. Integration

- [ ] 7.1 Run `dotnet test BoogaBooster.slnx` and `npm test`; verify both are green
- [ ] 7.2 Run the system via the Aspire AppHost, load and spin the ride to full power, and verify in the dashboard that queue happiness falls over time, rider bars populate on boarding, rider nausea climbs at sustained max G, and the bars show "No data" after offloading
- [x] 7.3 Run `openspec validate passenger-happiness --strict`; verify it passes
