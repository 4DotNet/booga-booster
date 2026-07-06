## Why

The Weather module already drives crowds (arrivals thin as the weather sours) but nothing protects the *ride* from the weather: a spinning rig full of passengers keeps running through a storm, and the security panel says nothing about it. Real rides close on safety grounds when conditions get bad enough. We need the weather to become a first-class safety interlock — visible to the operator, and able to force the ride shut and clear the line when it is no longer safe to run.

## What Changes

- The ride gains a **weather-safety interlock** graded into three levels derived from the Weather module's own `NiceWeather` indicator (which already folds severe wind and heavy precipitation into a single 0–1 severity): **Clear** (safe to operate), **Caution** (deteriorating — still running, operator warned), and **Unsafe** (too bad to run). The band thresholds are configurable.
- While the weather is **Unsafe**, the ride **cannot be loaded**: the `Idle → Loading` transition is blocked and the existing safety interlocks already gate `Loading → Safe` and `Safe → Started` through a new `UnsafeWeather` safety reason.
- When the weather crosses into **Unsafe**, the ride **automatically closes** (no operator command): a running ride performs a *controlled* stop (`Started → Stopping`) and, once it has come to a complete rest, unloads its passengers and returns to `Idle` via the existing `Stopping → Offloading → Idle` automatic transitions; a ride that is at rest with passengers aboard (`Loading`/`Safe`) goes straight to `Offloading` to disembark, then `Idle`.
- The **groups waiting in the ride's queue leave** when the weather becomes Unsafe — the line drains rather than waiting out a ride that will not run.
- The **security panel** shows a weather-safety indicator in its three states, conveyed by icon **and** text (never colour alone) and passing AXE / WCAG AA, driven by the authoritative server assessment carried on ride telemetry.
- **BREAKING (internal contract)**: `RideTelemetry` gains a weather-safety level and `RideSafetyReason` gains an `UnsafeWeather` member; the `DigitalTwin` module takes a new abstractions-only reference on `Weather.Abstractions` to read the current conditions.

## Capabilities

### New Capabilities

- `weather-safety-interlock`: The server-side ride behaviour — assessing the current weather into a graded safety level from the `NiceWeather` indicator (configurable thresholds), the new `UnsafeWeather` safety reason, blocking loading in unsafe weather, the automatic forced-closure transitions that stop and unload a running or loaded ride, and surfacing the weather-safety level on ride telemetry.
- `weather-safety-indicator`: The security panel's graded weather-safety indicator (Clear / Caution / Unsafe) driven by ride telemetry, conveyed by icon and text and meeting WCAG AA.
- `queue-weather-evacuation`: The Queue module draining a ride's waiting groups when the weather becomes unsafe, so queued guests leave a ride that has closed for the weather.

### Modified Capabilities

<!-- None — openspec/specs/ has no published baseline; the ride-state-machine, weather, and queue behaviours this builds on are delivered in their own (as-yet-unarchived) changes and are extended here as new capabilities. -->

## Impact

- **DigitalTwin module** (`src/DigitalTwin/FourDotnet.BoogaBooster.DigitalTwin`): the `Ride` aggregate factors a weather-safety level into its safety verdict and automatic transitions; a small weather-safety assessor (Application layer) maps `IWeatherConditionProvider` conditions to the graded level using configurable options; `RideSimulationService` reads the current weather each tick and applies it to the ride before advancing. Adds an **abstractions-only** `ProjectReference` to `Weather.Abstractions` (ADR-0004).
- **DigitalTwin.Abstractions**: `RideTelemetry` carries a new `WeatherSafety` level; a new `WeatherSafetyLevel` enum; `RideSafetyReason` gains `UnsafeWeather`.
- **Weather module**: consumes the existing `IWeatherConditionProvider` and `NiceWeather` indicator from `weather-service`; no change to the Weather contract.
- **Queue module** (`src/Queue/FourDotnet.BoogaBooster.Queue`): the existing weather-update handling gains a rule that evacuates all waiting groups when incoming conditions are unsafe, using the same `NiceWeather` closure threshold; `RideQueue` gains a clear/evacuate operation. Extends `QueueModuleOptions` with the closure threshold.
- **Frontend** (`src/FourDotnet.BoogaBooster.App`, `ride-dashboard/panels/security-panel`): a new weather-safety row bound to a `weatherSafety` telemetry field; the ride telemetry model/source gains the field.
- **Depends on**: `add-ride-state-machine` (lifecycle states, telemetry, security panel), `weather-service` (`IWeatherConditionProvider`, `NiceWeather`), `maintaining-the-people-queue` (the per-ride queue) and `queue-depend-on-weather` (the Queue module's weather-update handling that this extends).
- **Testing**: DigitalTwin tests for band mapping, blocked loading, and each forced-closure path (seeded, fake `TimeProvider`); Queue tests for evacuation on an unsafe update; Angular specs for the indicator's three states and its a11y.
- **Dependencies**: no new NuGet or npm packages.
