## 1. Domain: brake constants and brake torque

- [x] 1.1 In `RideParameters.cs`, add `MillBrakeTorque` (~`400_000`) and `HubBrakeTorque` (~`40_000`) constants alongside the mill/hub motor-and-loss groups, documented as brake actuators sized for a "stop within a couple of seconds" ramp-down.
- [x] 1.2 In `GreatMill`: add encapsulated `_brakesEngaged` state (default `false`), a `BrakesEngaged` getter, and `EngageBrakes()` (sets the flag and calls `CutAllPower()`) / `ReleaseBrakes()` (clears the flag) operations.
- [x] 1.3 In `GreatMill.AdvancePhysics`: add `RideParameters.MillBrakeTorque` to the Coulomb friction passed to `RotationalDynamics.Integrate` while `_brakesEngaged`, and thread the brake flag into `hub.AdvancePhysics`.
- [x] 1.4 In `Hub.AdvancePhysics`: accept a `brakesEngaged` argument and add `RideParameters.HubBrakeTorque` to the Coulomb friction passed to `Integrate` while engaged (leave `RotationalDynamics` unchanged).

## 2. Domain: Ride wiring (toggle + auto-brake)

- [x] 2.1 Replace `Ride.BrakeEngines()` with `Ride.SetEngineBrakes(bool engaged)` that calls `_mill.EngageBrakes()` / `_mill.ReleaseBrakes()` and `MarkChanged()`.
- [x] 2.2 In `EnterStopping` and `EnterEmergencyStop`, call `_mill.EngageBrakes()` (in place of the bare `CutAllPower()`), keeping the gondola-yaw-brake engagement.
- [x] 2.3 In `EnterStarted` and `EnterOffloading`, call `_mill.ReleaseBrakes()` so a fresh run and an at-rest offloading ride both report the brake released.

## 3. Application + telemetry contract

- [x] 3.1 In `IRideStore` / `RideStore`, replace `BrakeEngines()` with `SetEngineBrakes(bool engaged)` (same `Lock _gate` pattern, returning a telemetry snapshot).
- [x] 3.2 Add `BrakesEngaged` (bool) to `RideTelemetry`; set it from `_mill.BrakesEngaged` in `Ride.ToTelemetry()`.

## 4. API: command and endpoint

- [x] 4.1 Add `bool Engaged` to `BrakeEnginesCommand` and pass it through `BrakeEnginesCommandHandler` to `_store.SetEngineBrakes(command.Engaged)`.
- [x] 4.2 In `DigitalTwinEndpoints`, add a `SetEngineBrakeRequest(bool Engaged)` record and change `POST /ride/engine-brake` to bind it and dispatch `new BrakeEnginesCommand(request.Engaged)`.

## 5. Frontend: command dispatch and telemetry mapping

- [x] 5.1 In `ride.models.ts`: add `engaged: boolean` to `BrakeEnginesCommand`, add a `brakesEngaged` field to the ride-telemetry wire DTO, and read it into the UI model in `mapRideTelemetry`.
- [x] 5.2 In `sse-ride-telemetry-source.ts`: change `brakeEngines(engaged)` to POST `{ engaged }` to `ENGINE_BRAKE_URL`, keeping the optimistic echo (zero mill+hub power only when engaging, and echo `brakesEngaged`).
- [x] 5.3 In `ride-state.service.ts`: expose a `brakesEngaged` signal from telemetry and a toggle method (`setEngineBrakes`) dispatching `{ kind: 'brake-engines', engaged }`.

## 6. Frontend: operation controls

- [x] 6.1 In `operation-controls.ts`: make "Apply brakes" a toggle whose pressed state (and `aria-pressed`) follows `brakesEngaged()`, dispatching the negated state on click.
- [x] 6.2 Add `brakesEngaged()` to the condition that disables the mill/hub power-slider form, so the sliders disable while braking and re-enable when released (preserving the existing lifecycle gating).

## 7. Tests

- [x] 7.1 Domain physics tests: an engaged brake brings a spinning mill and hubs to rest within a couple of seconds; a released brake applies no braking torque; an engaged brake never spins a body up from rest.
- [x] 7.2 Domain tests: default brake is released; `SetEngineBrakes(true)` cuts mill+hub power and reports engaged; releasing does not restore power; engaging is a no-op when already engaged.
- [x] 7.3 Domain tests: entering `Stopping`/`Emergency Stop` engages the brake and cuts power; entering `Started`/`Offloading` releases it.
- [x] 7.4 Telemetry / store / endpoint tests: `RideTelemetry.BrakesEngaged` reflects the state; `RideStore.SetEngineBrakes` toggles it; `POST /ride/engine-brake` engages on `true` and releases on `false` (store + command-handler path).
- [x] 7.5 Frontend specs: `sse-ride-telemetry-source` posts the engaged flag; `mapRideTelemetry` carries `brakesEngaged`; `operation-controls` shows the toggle pressed and disables the power sliders while braking; `ride-state.service` toggle/signal covered.

## 8. Verify

- [x] 8.1 Run `dotnet test BoogaBooster.slnx` and `npm test`; fix regressions. (Backend 340 pass; frontend 147 pass. One pre-existing, unrelated failure remains — `ValueObjectTests.PassengerWeight_rejects_out_of_range_values(130.1)` vs `MaxPassengerKg=150` — untouched by this change.)
- [x] 8.2 Manually verify in the running app: start and spin up the ride, press "Apply brakes" — the sliders disable and the ride stops within a couple of seconds; release — the sliders re-enable; press Stop — the ride brakes to rest fast and offloads. (Requires the Aspire host + `ng serve`; left for a human run. Behaviour is covered by the automated domain/physics/telemetry and frontend suites above.)
