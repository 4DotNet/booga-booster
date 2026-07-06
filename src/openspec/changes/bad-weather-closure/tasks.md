## 1. DigitalTwin abstractions & wiring

- [ ] 1.1 Add a `WeatherSafetyLevel` enum (`Clear`, `Caution`, `Unsafe`) to `DigitalTwin.Abstractions`.
- [ ] 1.2 Add `UnsafeWeather` to the `RideSafetyReason` enum in `DigitalTwin.Abstractions`.
- [ ] 1.3 Add a `WeatherSafety` (`WeatherSafetyLevel`) field to the `RideTelemetry` record and its XML docs.
- [ ] 1.4 Add an abstractions-only `ProjectReference` from `FourDotnet.BoogaBooster.DigitalTwin` to `Weather.Abstractions` (ADR-0004).

## 2. Weather-safety assessment (Application layer)

- [ ] 2.1 Add a `WeatherSafetyOptions` (configurable Clear and closure thresholds on `NiceWeather`, defaults `0.5` / `0.25`).
- [ ] 2.2 Add an `IWeatherSafetyAssessor` plus an implementation that reads `IWeatherConditionProvider` and maps `NiceWeather` → `WeatherSafetyLevel` using the options.
- [ ] 2.3 Register the assessor and options in `AddDigitalTwinModule` (`DigitalTwinModuleExtensions`).
- [ ] 2.4 Have `RideSimulationService.Tick()` read the assessor and apply the level to the ride before advancing (extend `IRideStore`/`RideStore` as needed to pass the level through).

## 3. Ride aggregate behaviour

- [ ] 3.1 Add a stored weather-safety level to `Ride` and a `SetWeatherSafety(WeatherSafetyLevel)` method.
- [ ] 3.2 Factor `Unsafe` into `EvaluateSafety()` so it returns `RideSafetyReason.UnsafeWeather` (choose a sensible priority vs. the load/restraint reasons) and update `DescribeSafety`.
- [ ] 3.3 Add a guard to the `Idle → Loading` operator transition so it is blocked while the weather is `Unsafe`.
- [ ] 3.4 In `Advance`, add a weather-closure step (before the existing automatic transitions): `Started → Stopping` (controlled stop), and `Loading`/`Safe → Offloading`; leave `Idle`/`Stopping`/`EmergencyStop`/`Offloading` to finish naturally.
- [ ] 3.5 Populate `WeatherSafety` in `ToTelemetry()`; confirm the weather transitions never appear in `AvailableTransitions`.

## 4. DigitalTwin tests

- [ ] 4.1 Test band mapping in the assessor (Clear/Caution/Unsafe boundaries, configurable thresholds) with seeded conditions.
- [ ] 4.2 Test that `Idle → Loading`, `Loading → Safe`, and `Safe → Started` are rejected while `Unsafe`, and permitted again when cleared.
- [ ] 4.3 Test the running-ride closure path: `Started → Stopping → Offloading → Idle` under sustained `Unsafe` weather via `Advance` (fake `TimeProvider`).
- [ ] 4.4 Test the at-rest closure path: `Loading`/`Safe → Offloading → Idle`, and that `Idle` stays `Idle`.
- [ ] 4.5 Test telemetry reports the correct `WeatherSafety` level and `UnsafeWeather` reason, and that closure transitions are absent from `AvailableTransitions`.

## 5. Queue evacuation

- [ ] 5.1 Add an `Evacuate()` (clear-all) operation to the `RideQueue` domain model; ensure it leaves a valid empty queue and is a no-op when already empty.
- [ ] 5.2 Add a `WeatherClosureNiceThreshold` (default `0.25`) to `QueueModuleOptions`.
- [ ] 5.3 Extend the Queue module's weather-update handling so an update with `NiceWeather` below the threshold evacuates every ride's waiting groups (alongside the existing arrival suppression).
- [ ] 5.4 Add Queue tests: unsafe update drains the line, safe update leaves it intact, no arrivals refill while unsafe, arrivals resume after recovery, and `RideQueue.Evacuate()` behaviour.

## 6. Frontend security-panel indicator

- [ ] 6.1 Add the weather-safety level to the ride-telemetry model and its telemetry source, normalizing a missing/unknown value to `Clear`.
- [ ] 6.2 Add a `weatherSafety` input to `SecurityPanel` and render a third row (icon + worded state) for Clear/Caution/Unsafe; wire it from the ride-dashboard telemetry.
- [ ] 6.3 Ensure the state is conveyed by icon and text, not colour alone, and update panel styles.
- [ ] 6.4 Update/extend the simulated telemetry source so the panel can be exercised across the three states.
- [ ] 6.5 Add specs for the three states and telemetry-change tracking; update the security/telemetry-panel a11y specs to cover the new row (AXE / WCAG AA).

## 7. Verification

- [ ] 7.1 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` pass.
- [ ] 7.2 `npm test` (Vitest) passes in `FourDotnet.BoogaBooster.App`, including the new indicator and a11y specs.
- [ ] 7.3 End-to-end sanity: trigger a strong-wind/precipitation event, confirm the ride closes and unloads, the queue drains, the panel shows Unsafe, and everything recovers when the weather improves.
