# Tasks

## 1. Queue: guest mood on arrival

- [ ] 1.1 Create `docs/06-rider-experience.md` with an overview and a §6.1 "Guest mood on arrival" section covering the three 0–100 scores and the 65–85 / 50–100 / 0 draws, and link it from the reading-order table in `docs/README.md`. Verify the link resolves.
- [ ] 1.2 Extend `Queue/Domain/Person` with arrival happiness, preferred ride intensity and nausea, each checked against [0, 100], and add the arrival-range constants (`csharp-domain-model`). Verify with `Queue.Tests` cases for in-range scores and for happiness 101 and intensity −1 being rejected.
- [ ] 1.3 Draw happiness `Int(65, 85)` and intensity `Int(50, 100)` in `PersonGenerator.Next()` after name and weight, with nausea 0 (design D2). Verify with a 1,000-guest range test and a same-seed reproducibility test, and update any seeded expectations that shift in the same commit.

## 2. Queue: patience and status

- [ ] 2.1 Add a §6.2 "Queue patience" section to `docs/06` deriving `h(w)` (design D1) with the worked 10/15/20/25-minute losses, and add P, T and A to `docs/appendix-parameters.md` with units. Verify the worked values by hand in the doc.
- [ ] 2.2 Record `QueuedAt` on `QueuedGroup`, passed through `RideQueue.Enqueue`/`EnqueueAll` from `RideQueueService`'s `TimeProvider`. Verify the existing queue tests still pass and a new test asserts the recorded time.
- [ ] 2.3 Add `Person.HappinessAfterWaiting(TimeSpan)` with P, T and A as named constants. Verify with tests at 4:59 (80), 10/15/20 min (75/65/45), and 30 min from 65 (0).
- [ ] 2.4 Add `Happiness`, `PreferredRideIntensity` and `Nausea` to `PersonDto`, and `AverageHappiness` (`double?`) to `GetQueueStatusResponse` (`dto-organization`, design D3). Verify the solution builds.
- [ ] 2.5 Map current mood in `RideQueueService.ToDto` at `TimeProvider.GetUtcNow()` and compute the status average (`null` when empty). Verify with `FakeTimeProvider` tests: a group taken after 15 minutes reports 65 from 80, the average of 60/70/80 is 70, and an empty queue reports no average.
- [ ] 2.6 Run `Queue.Tests` and confirm Queue module line coverage stays ≥ 80 % (`test-coverage`).

## 3. DigitalTwin: passenger profile and boarding

- [ ] 3.1 Add the default-profile constants (happiness 75, preferred intensity 75) to `RideParameters`, each with a doc comment pointing to `docs/06`. Verify with a test that `PassengerProfile.Default` uses them.
- [ ] 3.2 Introduce the `PassengerProfile` value object (weight plus three scores validated to [0, 100]) and build `Passenger` from it. `PreferredRideIntensity` is fixed; `Happiness` and `Nausea` are private-set; `Passenger.OfWeight` uses the default profile (design D4). Verify with valid and out-of-range construction tests, and confirm the existing DigitalTwin tests still pass unchanged.
- [ ] 3.3 Replace `PassengerWeight` with `PassengerProfile` in `Ride.BoardGroup` and `IRideStore`/`RideStore.BoardGroup`, and map `PersonDto` mood in `RideLoadingCoordinator`. Verify with a coordinator test that boards a guest with 40/90/0 and asserts that the seated passenger has the same mood.

## 4. DigitalTwin: riding mood

- [ ] 4.1 Add a §6.3 "Ride intensity and riding mood" section to `docs/06` deriving felt-G intensity (D5), the comfort band and the exact-step nausea integration with the 20/40/60 s values (D6), and the in-motion-only rule (D8). Add `R_h`, `B`, the 30 pp threshold, `k` and `N₀` to `RideParameters` and `docs/appendix-parameters.md`. Verify every constant in the doc appears in the appendix with a unit, and no new literal appears in the domain code.
- [ ] 4.2 Implement `Passenger.ExperienceIntensity(intensity, dt)` (comfort-band gain, exponential nausea at Δ ≥ 30, both clamped). Verify with tests for 50→50 and 100→100 over 10 s (+10), 80 vs 72.5 (+5), 100 vs 50 (0), the cap at 100, nausea ≈ 3/15/63 at 20/40/60 s, Δ = 29 (0), and the nausea cap.
- [ ] 4.3 Compute `G_felt` and intensity in `Gondola.AdvancePhysics` after the G update, and pass intensity to each occupant through `Seat`. Verify with tests at 2.25 g (50), 5.0 g (100) and rest (≈22), and that an empty seat is skipped.
- [ ] 4.4 Verify through `Ride.Advance` that 60 s of `Loading` leaves mood unchanged, that `Started` changes it, and that two identical rides given the same commands produce identical moods (determinism).

## 5. DigitalTwin: over-G shock

- [ ] 5.1 Add a §6.4 "Over-G shock" section to `docs/06` explaining the once-per-episode reading (D7), and add the 1 s hold and +25 shock to `RideParameters` and the appendix. Verify the appendix entries.
- [ ] 5.2 Track over-G episodes in `Gondola` (accumulate while `G_felt ≥ G_max`, reset below, shock once after 1 s) and add `Passenger.SufferOverGShock()`. Verify with tests for a 3 s hold (one shock, +25), a 0.5 s peak (none), two 1.5 s episodes (two shocks) and the cap at 100.

## 6. DigitalTwin: telemetry summary

- [ ] 6.1 Add `RiderExperienceTelemetry(RiderCount, AverageHappiness?, AveragePreferredRideIntensity?, AverageNausea?)` to `DigitalTwin.Abstractions` and as `RiderExperience` on `RideTelemetry`, built in `Ride.ToTelemetry()` (design D9). Verify with the 60/80/70, 50/100/75, 0/20/10 → 3 riders, 70/75/10 test and an empty-ride test (0 riders, no averages).
- [ ] 6.2 Verify with an endpoint/stream test that the SSE frame and `GET /ride/telemetry` serialise `riderExperience` with `null` averages when empty, then run `DigitalTwin.Tests` and confirm module line coverage ≥ 80 %.
- [ ] 6.3 Run the `api-contract-guardian` audit for `PersonDto`, `GetQueueStatusResponse` and `RideTelemetry`. Verify it reports only the drift that group 7 will close.

## 7. Frontend: models and state

- [ ] 7.1 Add `averageHappiness: number | null` to the queue status model and mapping in `queue/models/queue.models.ts`, with an absent field mapped to `null`, and expose it as a signal on `QueueStateService`. Verify with `queue.models.spec.ts` (present, `null`, absent) and `queue-state.service.spec.ts`.
- [ ] 7.2 Add the rider-experience summary to `ride-dashboard/models/ride.models.ts`, with an absent summary mapped to 0 riders and `null` averages, and expose it as a signal on `RideStateService`. Verify with `ride.models.spec.ts` and `ride-state.service.spec.ts`.

## 8. Frontend: Rider Experience panel

- [ ] 8.1 Consult the `primeng` MCP server (`ProgressBar` API, theming, accessibility; `providePrimeNG` setup) and add `providePrimeNG` with a `@primeuix/themes` preset to `app.config.ts`. Verify `npm run build` succeeds and the existing specs still pass.
- [ ] 8.2 Build `ride-dashboard/panels/rider-experience-panel` (`angular-architect`: OnPush, four `input<number | null>()`, PrimeNG `p-progressbar`, design D10). It shows four bars in spec order with rounded visible values, "No guests waiting"/"No riders" for `null`, an accessible name and "N out of 100" value text, and a polite live region. Verify with a Vitest spec covering the values, the rounding, both no-data states and live input updates.
- [ ] 8.3 Add `rider-experience-panel.a11y.spec.ts` (axe-core) for the with-values and no-data renders. Verify zero violations.
- [ ] 8.4 Place the panel under the Gondolas panel in `ride-dashboard.html`, bound to the queue and ride state signals. Verify with a dashboard spec asserting order and binding, and confirm the dashboard a11y spec still passes.
- [ ] 8.5 Run `angular-critic` on the frontend diff and `csharp-critic` on the backend diff, and resolve every blocking and major finding. Verify that a re-run reports none.

## 9. Integration check

- [ ] 9.1 Run `dotnet build` and `dotnet test BoogaBooster.slnx` from `src/`, and `npm test` in the Angular app. Verify all of them are green.
- [ ] 9.2 Run the system via the Aspire AppHost and drive a ride from the dashboard. Verify that queue happiness appears, that the rider bars appear after boarding and change while the ride runs, and that the empty states read "No guests waiting" / "No riders".
