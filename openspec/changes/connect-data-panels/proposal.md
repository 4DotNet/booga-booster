## Why

The top-right "Load & security" panel (and the top-left status summary) do not reflect the ride's real state: the boarded-passenger count is wrong and the weight in kilos never updates. The cause is structural — the telemetry SSE stream is silent during exactly the phase where these numbers change (boarding/loading), and the frontend reconstructs load and passenger count from per-seat data that it never receives in that phase. Boarding must also feel physical: a passenger grabs a random gondola, takes a seat, and the restraint takes time to become safe, each such change pushing a fresh SSE frame that re-renders the panels.

## What Changes

- **Stream telemetry while the ride is loading and safe, not only while running.** The telemetry stream currently emits frames only in the running states (`Started`/`Stopping`/`EmergencyStop`). It SHALL also emit while the ride is `Loading`, `Safe`, and `Offloading`, so panels observe passengers boarding, load/weight accumulating, and restraints securing live. **BREAKING** for the existing "emission gated on the running ride" requirement.
- **Passengers board a randomly chosen empty gondola.** Group boarding currently seats members into the first empty gondolas in a fixed hub-by-hub order. It SHALL instead choose empty gondolas at random, so load distributes unpredictably across the mill — while preserving the existing whole-gondola, never-split-a-group, `ceil(N/2)`-gondolas rules.
- **Surface a boarded-passenger count and per-seat occupancy/secured state in telemetry** so the frontend reports the count directly instead of guessing from `occupiedKg`, and can show a seat as occupied-but-not-yet-secured during the restraint countdown.
- **Bind the panels to the streamed load/weight/passenger data** so the security panel's passenger count, weight-in-kilos, security roll-up, and load balance update on every frame throughout loading.

## Capabilities

### New Capabilities
<!-- None. This change corrects and extends behavior already owned by existing capabilities. -->

### Modified Capabilities
- `ride-telemetry-streaming`: emission is no longer gated on the running state — frames are also emitted during `Loading`, `Safe`, and `Offloading`; the telemetry payload gains an explicit boarded-passenger count and per-seat occupied/secured indicators; panels bind their passenger, weight, and security readings to this streamed data.
- `ride-loading`: passengers board into a randomly selected empty gondola rather than the first empty gondola in fixed order (all fit/capacity rules unchanged).

## Impact

- **Backend — DigitalTwin module:**
  - `Application/RideTelemetryStream.cs` — emission gate widened beyond `IsRunning`.
  - `Domain/Ride.cs` (`BoardGroup`) and `Domain/GreatMill.cs` (`EmptyGondolas`) — random empty-gondola selection.
  - `Domain/Ride.cs`, `Domain/GreatMill.cs`, `Domain/Hub.cs`, `Domain/Gondola.cs`, `Domain/Seat.cs` (`ToTelemetry`) — populate new count/occupancy/secured fields.
- **Abstractions — DigitalTwin.Abstractions:** `RideTelemetry`, `GondolaTelemetry`, `SeatTelemetry` (and possibly `HubTelemetry`/`MillTelemetry`) records gain boarded-count and per-seat occupied/secured members. Contract change consumed by the frontend.
- **Frontend — FourDotnet.BoogaBooster.App:** `data/sse-ride-telemetry-source.ts` (`mapRideTelemetry`), `models/ride.models.ts` (DTO + derived signals), `state/ride-state.service.ts`, and the `security-panel` / `status-summary` panels bind to the streamed count/weight.
- **Tests:** DigitalTwin domain tests (boarding randomness, telemetry projection), telemetry-stream emission tests, and Angular SSE-source / panel specs.
