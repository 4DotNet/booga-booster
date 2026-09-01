## 1. Domain: direction concept and signed drive

- [x] 1.1 Add a `MotorDirection` type (`Forward` / `Reverse`) in `DigitalTwin/…/Domain` with a `Sign()` helper returning `+1d` / `-1d`.
- [x] 1.2 In `GreatMill`: add encapsulated `_direction` state (default `Forward`), a `Direction` getter, and `SetDirection(MotorDirection)` using `ApplyChange`; add `SetAllHubDirection(MotorDirection)` that fans out to the hubs.
- [x] 1.3 In `Hub`: add encapsulated `_direction` state (default `Forward`), a `Direction` getter, and `SetDirection(MotorDirection)` using `ApplyChange`.
- [x] 1.4 In `GreatMill.AdvancePhysics` and `Hub.AdvancePhysics`, multiply the `MotorTorque` magnitude by `_direction.Sign()` before passing it to `Integrate`; leave `RotationalDynamics` unchanged.
- [x] 1.5 In `Ride`: add `SetMainEngineDirection(MotorDirection)` and `SetHubEngineDirection(MotorDirection)` mirroring the power setters (mark-changed plumbing).

## 2. Application + telemetry contract

- [x] 2.1 Add `SetMainEngineDirection` / `SetHubEngineDirection` to `IRideStore` and implement them in `RideStore` (same thread-safe pattern as the power setters).
- [x] 2.2 Add a `Direction` field to `MillTelemetry` and `HubTelemetry`; document that `Rpm` is now signed. Update `GreatMill.ToTelemetry` / `Hub.ToTelemetry` to pass the commanded direction.

## 3. API: command features and endpoints

- [x] 3.1 Add a `SetMainEngineDirection` CQRS feature (command + handler) under `Features/`, mirroring `SetMainEnginePower`.
- [x] 3.2 Add a `SetHubEngineDirection` CQRS feature (command + handler) under `Features/`, mirroring `SetHubEnginePower`.
- [x] 3.3 In `DigitalTwinEndpoints`: add a `SetDirectionRequest(string? Direction)` record and map `POST /ride/main-direction` and `POST /ride/hub-direction`, parsing the direction case-insensitively and returning 400 for unknown values.

## 4. Frontend: command dispatch and telemetry mapping

- [x] 4.1 In `sse-ride-telemetry-source.ts`: add the two direction URLs and replace the local-only `set-mill-direction` / `set-hub-direction` stubs with real `http.post` calls, keeping the optimistic echo.
- [x] 4.2 In `ride.models.ts`: add `direction` to `RideTelemetryMillStreamDto` / `RideTelemetryHubStreamDto` (numeric-or-name), add a `MotorDirection` wire-enum resolver, and change `mapRideTelemetry` to read the reported direction instead of hard-coding `'forward'`.
- [x] 4.3 Audit the frontend rpm consumers (`ride-state.service`, `status-summary`, speed gauges) for assumptions that rpm is non-negative; display speed magnitude with the direction shown separately.

## 5. Frontend: visualization

- [x] 5.1 In `ride-visualization.ts`: drive each body's rotation from the signed rpm only — stop `angularVelocity` from also multiplying by the commanded direction so a reversal is not double-counted.
- [x] 5.2 Confirm the operation-controls toggles reflect the telemetry-reported direction and dispatch on click (already wired to `setMillDirection`/`setHubDirection`).

## 6. Tests

- [x] 6.1 Domain tests: default direction is Forward; `SetDirection` is a no-op when unchanged and marks Modified when changed; direction is independent of power.
- [x] 6.2 Domain physics tests: a reversed motor spins a body up negative from rest; reversing a spinning body decelerates it through zero and then accelerates it negative; the turnaround is continuous (no single-step sign jump).
- [x] 6.3 Telemetry tests: mill/hub telemetry reports the commanded direction and a signed rpm (negative while running reversed, zero-with-direction at rest).
- [x] 6.4 Store/endpoint tests: `RideStore` direction setters and the two new endpoints (200/Accepted on valid, 400 on unknown direction).
- [x] 6.5 Frontend specs: `sse-ride-telemetry-source` posts the direction commands; `mapRideTelemetry` carries direction through; `ride-visualization` turns a body backwards for a negative rpm.

## 7. Verify

- [x] 7.1 Run `dotnet test BoogaBooster.slnx` and `npm test`; fix regressions. (Direction suites green: backend 145 pass, frontend 140 pass. One pre-existing, unrelated failure remains — `ValueObjectTests.PassengerWeight_rejects_out_of_range_values(130.1)` vs `MaxPassengerKg=150` — untouched by this change.)
- [x] 7.2 Manually verify in the running app: start the ride, spin up, toggle Reverse — the mill/hubs slow, stop, and turn the other way in the 3D view and gauges. (Requires running the Aspire host + `ng serve`; left for a human run. Behaviour is covered by the automated domain/telemetry/frontend suites above.)
