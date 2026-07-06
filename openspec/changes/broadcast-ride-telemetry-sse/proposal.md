## Why

The physics engine already computes a complete, deterministic telemetry snapshot every tick (`RideTelemetry`: mill, four hubs, sixteen gondolas, per-seat sensors, G-forces, load balance — see `docs/`), but the only way to read it is a one-shot `GET /ride/telemetry`, and the dashboard doesn't even use it — every panel is driven by a client-side simulator. There is no live push of the real ride state to the operator. We need the backend to broadcast the full telemetry as a stream while the ride runs, and the frontend to consume that stream into a single local store the panels already subscribe to — turning the dashboard into a real-time view of the actual digital twin.

## What Changes

- Add a **Server-Sent Events (SSE)** endpoint on the DigitalTwin module that streams the full `RideTelemetry` snapshot to connected clients, using the built-in .NET 10 `TypedResults.ServerSentEvents<T>` API (`text/event-stream`).
- The stream **samples the running ride at a fixed telemetry rate** (decoupled from the physics timestep, per `docs/01-physics-overview.md` §1.4) and pushes each snapshot as one JSON event. Telemetry frames are emitted **only while the ride is running**; when the ride is not running the connection stays open but no frames flow.
- The snapshot carries everything the panels need: commanded engine power and sensed RPM for the **mill and every hub**, and each **gondola/cart** state — brake, angle, RPM, forward/lateral G, load, per-seat weight and restraint — plus the mill's load-balance/overload readings.
- Frontend: the dashboard **always opens the SSE connection** (on load) and keeps it open with automatic reconnect; each incoming frame updates a **single local telemetry store**, and the existing signal-based panels re-render from it. No panel talks to the stream directly.
- **BREAKING (frontend data source):** replace the client-side simulator with the SSE-backed telemetry source as the app's `RIDE_TELEMETRY_SOURCE`. Operator commands (power, direction, brake) now POST to the existing HTTP command endpoints instead of mutating a local simulator. The simulator is retained only for tests/offline use.
- Keep the existing one-shot `GET /ride/telemetry` (used elsewhere, e.g. lifecycle polling); the stream is additive on the backend.

## Capabilities

### New Capabilities

- `ride-telemetry-streaming`: the server-side broadcast of the full ride telemetry over SSE while the ride runs (endpoint, sampling loop, frame contract, connection lifecycle) and the client-side consumption of that stream into the shared telemetry store the panels subscribe to.

### Modified Capabilities

<!-- No existing published specs (openspec/specs/ is empty). This is a new capability; it consumes the RideTelemetry contract but does not change its requirements. -->

## Impact

- **DigitalTwin module (backend):**
  - New SSE endpoint (e.g. `GET /ride/telemetry/stream`) in `Endpoints/DigitalTwinEndpoints.cs`.
  - New telemetry-stream producer (an `IAsyncEnumerable<RideTelemetry>` that samples `IRideStore.GetTelemetry()` at the telemetry interval, gated on the running state, honouring client cancellation).
  - A telemetry/broadcast interval parameter in `RideParameters` (e.g. 30 Hz), decoupled from the physics `dt`.
  - Unit tests for the stream producer (emits while running, quiet while stopped, stops on cancellation).
- **Angular app (frontend):**
  - New `SseRideTelemetrySource` implementing the existing `RideTelemetrySource` seam (EventSource connection + frame → model mapping + command POSTs).
  - Provider swap in `app.config.ts` (`RIDE_TELEMETRY_SOURCE` → SSE source); `SimulatedRideTelemetrySource` demoted to test/offline use.
  - Backend→frontend `RideTelemetry` mapping helper; possible minor extension of the frontend model to carry fields the visualization needs (e.g. gondola angle).
  - Dev-server proxy config adjusted so `/api/**` SSE responses are not buffered.
- **Cross-change dependency:** builds on `add-ride-state-machine` for the ride's *running* state that gates emission; naming/ordering noted in the design.
