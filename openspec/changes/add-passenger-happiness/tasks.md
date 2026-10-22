# Tasks

## 1. Specification doc

- [x] 1.1 Write `docs/06-rider-experience.md` deriving the mood model: the queue-wait decay (D1), felt-G intensity (D4), the happiness comfort band (D5), exact-step nausea integration with the worked 20/40/60 s values (D6), the over-G episode rule (D7), and the in-motion-only update rule (D8). Link it from `docs/README.md`. Verify the worked numbers by hand calculation in the doc.
- [x] 1.2 Add the new constants (patience threshold, decay half-life, default profile, comfort band, gain rate, nausea threshold/doubling/seed, over-G hold and shock) to `docs/appendix-parameters.md`. Verify every constant named in the doc appears in the table with a unit.

## 2. Queue: guest mood domain

- [x] 2.1 Extend `Queue/Domain/Person` with arrival happiness, preferred ride intensity and nausea, validated to [0, 100] (`csharp-domain-model`). Verify with `Queue.Tests` cases for valid and out-of-range scores.
- [x] 2.2 Add the read-time happiness function `Person.HappinessAfterWaiting(TimeSpan)` with the 5-minute threshold and 10-minute half-life as named constants. Verify with tests at 4:59 (unchanged), 15:00 (halved) and a strictly decreasing 10/20/30-minute series.
- [x] 2.3 Record `QueuedAt` on `QueuedGroup`, passed through `RideQueue.Enqueue`/`EnqueueAll`. Verify the existing queue tests still pass and that a new test asserts the recorded time.
- [x] 2.4 Draw initial mood in `PersonGenerator` (happiness 65–85, intensity 50–100, nausea 0) after name and weight. Verify a 1,000-guest range test and a same-seed reproducibility test, and update any seeded expectations that shift.

## 3. Queue: contract and status

- [x] 3.1 Add `Happiness`, `PreferredRideIntensity` and `Nausea` to `PersonDto` and `AverageHappiness` (`double?`) to `GetQueueStatusResponse` (`dto-organization`). Verify the solution builds.
- [x] 3.2 Map the current (decayed) mood in `RideQueueService.ToDto` using `TimeProvider`, and compute the status average (`null` when empty). Verify with `FakeTimeProvider` tests: a group taken after 15 minutes reports halved happiness, the average over 60/70/80 is 70, and an empty queue reports no average.
- [x] 3.3 Verify `dotnet test` passes for `Queue.Tests` and that Queue module coverage stays ≥ 80 % (`test-coverage`).

## 4. DigitalTwin: passenger mood

- [x] 4.1 Add the mood constants and the default profile (75/75/0) to `RideParameters`, each with a doc comment pointing to `docs/06-rider-experience.md`. Verify no new literal appears in the domain code.
- [x] 4.2 Introduce the `PassengerProfile` value object (weight plus three validated 0–100 scores). Verify with tests for valid and out-of-range construction.
- [x] 4.3 Extend `Passenger` with fixed `PreferredRideIntensity` and private-set `Happiness`/`Nausea`, plus `ExperienceRide(intensity, dt)` (comfort-band gain, exact-step exponential nausea past +30 pp, clamped) and `SufferOverGShock()`. Keep `Passenger.OfWeight` on the default profile. Verify with tests for the 10 s / +10 gain, the cap at 100, no gain when mismatched, nausea ≈ 3/15/63 at 20/40/60 s, and holding below the threshold.
- [x] 4.4 Replace `PassengerWeight` with `PassengerProfile` in `Ride.BoardGroup` and the store, and map `PersonDto` mood in `RideLoadingCoordinator`. Verify a coordinator test boards a member with happiness 40 / intensity 90 and the seat's passenger reports the same values.

## 5. DigitalTwin: intensity and over-G

- [x] 5.1 Compute felt G and intensity per gondola (D4) after the G-force update. Verify tests at 2.25 g → 50 and 5.0 g → 100.
- [x] 5.2 Track over-G episodes in `Gondola` (accumulate at or above `G_max`, reset below, shock once past 1 s) and apply the mood update to occupants on every physics step. Verify tests for a 3 s hold (one shock), a 0.5 s peak (none) and two 1.5 s episodes (two shocks).
- [x] 5.3 Drive mood updates from `Ride.Advance` only in `Started`/`Stopping`/`EmergencyStop`. Verify that 60 s of `Loading` leaves mood unchanged and that two identical rides replaying the same commands produce identical mood (determinism).

## 6. DigitalTwin: telemetry summary

- [x] 6.1 Add `RiderExperienceTelemetry(RiderCount, AverageHappiness?, AveragePreferredRideIntensity?, AverageNausea?)` to `DigitalTwin.Abstractions` and to `RideTelemetry`, and populate it in `Ride.ToTelemetry()`. Verify the 60/80, 50/100, 0/20 → 70/75/10 test and the empty-ride → 0 riders, no averages test.
- [x] 6.2 Verify the SSE stream and the `GET` telemetry endpoint serialise the new field (endpoint/stream test), and that `DigitalTwin.Tests` pass with module coverage ≥ 80 %.

## 7. Frontend: models and state

- [ ] 7.1 Add `averageHappiness: number | null` to the queue DTO/model mapping in `queue.models.ts`, treating a missing field as `null`. Verify `queue.models.spec.ts` cases for present, `null` and absent values.
- [ ] 7.2 Add the rider-experience summary to `ride.models.ts` and expose it as a signal on `RideStateService`, with a missing summary mapped to `null` averages. Verify with `ride.models.spec.ts` and `ride-state.service.spec.ts` cases.

## 8. Frontend: Rider Experience panel

- [ ] 8.1 Consult the `primeng` MCP server for `ProgressBar` (API, theming, accessibility) and confirm the PrimeNG theme provider is configured in `app.config.ts`. Verify by running `npm run build` cleanly.
- [ ] 8.2 Build `ride-dashboard/panels/rider-experience-panel` (`angular-architect`: OnPush, `input()` signals, PrimeNG `p-progressbar`). It shows four labelled bars in spec order with rounded visible values, "No guests" for `null`, and an accessible name plus value text on each bar. Verify with a Vitest spec covering values, the no-data states and live updates.
- [ ] 8.3 Add `rider-experience-panel.a11y.spec.ts` (axe-core) for both the with-values and no-guests renders. Verify it reports zero violations.
- [ ] 8.4 Place the panel under the Gondolas panel in `ride-dashboard.html`, bound to the queue and ride state signals. Verify with a `ride-dashboard.spec.ts` assertion on order and binding, and confirm `ride-dashboard.a11y.spec.ts` still passes.

## 9. Integration check

- [ ] 9.1 Run `dotnet build` and `dotnet test BoogaBooster.slnx` from `src/`, and `npm test` in the Angular app. Verify everything is green.
- [ ] 9.2 Run the system via the Aspire AppHost and drive a ride from the dashboard. Verify that queue happiness shows within the first minute, rider bars appear after boarding and move while the ride runs, and empty states read "No guests".
