# Tasks

## 1. Mood model specification (docs)

- [ ] 1.1 Write `docs/06-rider-experience.md`, deriving: the queue-wait decline h(w) (D1, with the 10/15/20/25-minute table); intensity = √(F²+L²+1)/G_max (D5); the triangular happiness band (D6); the exact-step offset-exponential nausea (D6, with the 20/40/60 s table); and the once-per-episode over-G shock (D7). Also record the intensity range normal running reaches (about 22–62). Verify that every number in the doc matches the scenarios in `specs/guest-mood` and `specs/rider-experience`.
- [ ] 1.2 Add every new constant (P, T, A, R_h, B, k, N₀, the over-G dwell of 1 s and shock of 25, the neutral mood 75/75/0) to `docs/appendix-parameters.md` with its value and rationale, and link doc 06 from `docs/README.md`. Verify that each constant name in the appendix appears in doc 06.

## 2. Queue domain: guest mood

- [ ] 2.1 Extend `Queue/Domain/Person` with `ArrivalHappiness`, `PreferredRideIntensity` and `Nausea`, validated to [0, 100] (`DomainValidationException`), plus the named arrival-range constants (D2). Update the existing `Person` tests and add out-of-range cases. Verify `dotnet test` passes for `FourDotnet.BoogaBooster.Queue.Tests`.
- [ ] 2.2 Add `Person.HappinessAfterWaiting(TimeSpan wait)` implementing D1 with named constants. Verify with tests at 4:59, 10, 15, 20, 25 and 30 minutes (expected 75, 70, 60, 40, 0 and 0 for h₀ = 75).
- [ ] 2.3 Add `QueuedAt` to `QueuedGroup` and pass the enqueue time through `RideQueue.EnqueueAll`, without the domain reading a clock. Verify with a `RideQueue` test that every group from one `EnqueueAll` call has the supplied time.
- [ ] 2.4 Draw happiness (65–85) and preferred intensity (50–100) in `PersonGenerator.Next()` after name and weight, with nausea 0 (D2). Verify with a 1,000-person range test and a same-seed reproducibility test, and update any seeded expectations that shift.

## 3. Queue contract and service

- [ ] 3.1 Add `Happiness`, `PreferredRideIntensity` and `Nausea` to `PersonDto` and `AverageHappiness` (nullable) to `GetQueueStatusResponse` (D3). Run the `dto-organization` skill check, and verify that the solution builds.
- [ ] 3.2 In `RideQueueService`, stamp `QueuedAt` from `TimeProvider` on enqueue. Evaluate `HappinessAfterWaiting` at `GetUtcNow()` in `ToDto`, and compute the average (`null` when empty) in `GetStatus`. Verify with `FakeTimeProvider` tests that status and `TakeGroupAsync` report the declined happiness and the correct average, and `null` for an empty queue.
- [ ] 3.3 Verify that the Queue module library is still at or above 80 % line coverage (`test-coverage` skill or `test-runner`).

## 4. DigitalTwin domain: passenger mood on the ride

- [ ] 4.1 Add the mood constants to `DigitalTwin/Domain/RideParameters.cs` with doc comments that point to `docs/06`. Verify that no literal tuning number appears in the new domain code (critic check in 4.7).
- [ ] 4.2 Add the `PassengerProfile` value object with [0, 100] validation and `Neutral(weight)` (D4). Verify with validation and factory unit tests.
- [ ] 4.3 Build `Passenger` from a `PassengerProfile`. Add private-set `Happiness`/`Nausea`, `ExperienceIntensity(intensity, dt)` (D6) and `SufferOverGShock()` (D7), clamped to [0, 100], and keep `OfWeight` working. Verify with tests for the spec scenarios: full, partial and no gain; nausea of 15 after 40 s at gap 30; no nausea at gap 29 or with a negative gap; the caps.
- [ ] 4.4 Add `Seat.ExperienceIntensity` and `Seat.SufferOverGShock` pass-throughs that do nothing on an empty seat. Verify with seat unit tests.
- [ ] 4.5 In `Gondola.AdvancePhysics`, compute `G_felt` and `Intensity` after `UpdateGForces`, apply them to both seats, and track the over-G episode accumulator (D5, D7). Verify with tests for: 2.25 g → 50, 5 g → 100, rest → about 22; 3 s at 4.6 g → +25 once; 0.9 s → no shock; two episodes → +50.
- [ ] 4.6 Verify with a `Ride` test that a seated passenger's mood is unchanged after advancing in `Loading`, `Safe` and `Offloading`, and changes in `Started` (D8).
- [ ] 4.7 Run the `csharp-critic` agent on groups 2–4 and fix every blocking or major finding.

## 5. DigitalTwin boarding and telemetry

- [ ] 5.1 Change `Ride.BoardGroup` and `IRideStore.BoardGroup`/`RideStore` from `PassengerWeight` to `PassengerProfile`. In `RideLoadingCoordinator`, map each `PersonDto`'s mood and weight onto a profile. `BoardPassenger` uses `PassengerProfile.Neutral`. Verify by updating the existing boarding tests and adding a coordinator test showing that the taken guest's happiness, preference and nausea arrive on the seat.
- [ ] 5.2 Add `RiderExperienceTelemetry` to `DigitalTwin.Abstractions`, append `RiderExperience` to `RideTelemetry`, and build it in `Ride.ToTelemetry()` with `null` averages for an empty ride (D9). Verify with tests for the two-rider averages scenario and the empty-ride scenario, and check that `RiderCount == BoardedPassengerCount`.
- [ ] 5.3 Verify with an endpoint or stream test that the GET telemetry and the SSE frame both serialise `riderExperience` in camelCase.
- [ ] 5.4 Verify that the DigitalTwin module library is still at or above 80 % line coverage, then run `csharp-critic` on group 5 and fix every blocking or major finding.

## 6. Frontend contract

- [ ] 6.1 Run `api-contract-guardian` in fix mode for the new `PersonDto`, `GetQueueStatusResponse` and `RideTelemetry` fields. It updates the TypeScript DTO interfaces and mappers, mapping missing fields to `null`. Verify that the guardian reports no drift and that the mapper specs cover both present and missing values.
- [ ] 6.2 Expose `averageHappiness` on `QueueStateService` and a `riderExperience` computed signal on `RideStateService`. Verify with state-service specs.

## 7. Rider Experience panel

- [ ] 7.1 Add `providePrimeNG` with a `@primeuix/themes` preset to `app.config.ts`, after checking the setup with the `primeng` MCP server (`get_setup`). Verify that `npm run build` succeeds and that existing specs still pass.
- [ ] 7.2 Create `ride-dashboard/panels/rider-experience-panel` (`bb-rider-experience-panel`): OnPush, four `input<number | null>()` signals, PrimeNG `p-progressbar` rows with `showValue` off, visible labels, rounded values, `aria-labelledby`, "N out of 100" value text, a no-data state for `null`, and a polite live region (D10). Validate the usage with the `primeng` MCP server's `validate_usage`. Verify with a Vitest spec for value rounding, the fill amount and the no-data state.
- [ ] 7.3 Add `rider-experience-panel.a11y.spec.ts` (axe-core), covering the panel with data and without data. Verify that it reports zero violations, including colour contrast.
- [ ] 7.4 Place the panel in a new `.panel` directly after the gondola panel in `ride-dashboard.html` and bind it to the two state services. Verify with `ride-dashboard.spec.ts` (placement and binding) and `ride-dashboard.a11y.spec.ts` (still clean).
- [ ] 7.5 Run the `angular-critic` agent on groups 6–7 and fix every blocking or major finding.

## 8. Integration check

- [ ] 8.1 Run `dotnet test BoogaBooster.slnx` and `npm test` (through `test-runner`). Verify that both are green and that the Queue and DigitalTwin libraries are at or above 80 % line coverage.
- [ ] 8.2 Run the AppHost, start the ride with a loaded queue, and confirm that the Rider Experience panel shows queue happiness falling after 5 minutes, rider bars filling after boarding, and no-data states when the queue or ride is empty.
