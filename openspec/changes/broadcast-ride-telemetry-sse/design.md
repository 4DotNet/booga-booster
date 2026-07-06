## Context

The DigitalTwin module runs a deterministic, fixed-timestep physics loop in `RideSimulationService` (a `BackgroundService`) that mutates a single in-memory `Ride` aggregate held behind `IRideStore`. `IRideStore.GetTelemetry()` returns a consistent, immutable `RideTelemetry` snapshot (mill + 4 hubs + 16 gondolas + per-seat sensors + G-forces + load balance). Today that snapshot is only reachable via a one-shot `GET /ride/telemetry`, and the Angular dashboard ignores it entirely — every panel is fed by `SimulatedRideTelemetrySource` through the `RIDE_TELEMETRY_SOURCE` seam, with `RideStateService` projecting that one `telemetry` signal into per-panel signals.

`docs/01-physics-overview.md` §1.4 is explicit that **telemetry is sampled/pushed on its own schedule (e.g. 30 Hz), independent of the physics `dt` (1/120 s)** using an accumulator — so the render/telemetry rate must not be coupled to the integrator.

.NET 10 ships first-class SSE support: `TypedResults.ServerSentEvents<T>(IAsyncEnumerable<T> values, string? eventType)` (and an `IAsyncEnumerable<SseItem<T>>` overload) serializes each item as a `text/event-stream` `data:` frame using the app's JSON options. The browser's native `EventSource` consumes it with built-in auto-reconnect.

Per the decisions for this change: the **frontend always connects** to the stream (even at rest), the **backend only emits frames while the ride is running**, and the SSE source **replaces** the simulator as the app's telemetry source.

**IMPORTANT:** all C# must follow the `4dotnet-csharp-style-guide` MCP server — ADR-0002 (minimal APIs), ADR-0007 (endpoints live in the module via `MapDigitalTwinEndpoints`), ADR-0004 (module/abstractions boundary), and the xUnit v3 / Moq / Bogus testing guideline. Consult it before writing code.

## Goals / Non-Goals

**Goals:**

- Stream the full `RideTelemetry` snapshot over SSE at a fixed, physics-decoupled telemetry rate, emitting only while the ride runs.
- Keep the connection open across the ride's idle periods so the client never has to reconnect on each run.
- Feed a single client-side store from the stream; leave the existing panels/signals untouched.
- Route operator commands over the existing HTTP command endpoints (the stream is read-only).

**Non-Goals:**

- Changing the `RideTelemetry` contract or the physics (this change is transport + wiring only).
- Delta/compressed frames, binary protocols, or WebSockets — full JSON snapshots over SSE are sufficient at this scale (one ride, few clients).
- Event replay / `Last-Event-ID` resume — telemetry is a live snapshot, so a reconnect simply resumes with the current state.
- Multi-ride fan-out or authentication on the stream.
- Retiring the one-shot `GET /ride/telemetry` (kept for lifecycle polling and diagnostics).

## Decisions

### 1. Endpoint returns `TypedResults.ServerSentEvents<RideTelemetry>` over a sampling `IAsyncEnumerable`

Add `GET /ride/telemetry/stream` to `DigitalTwinEndpoints`. It returns `TypedResults.ServerSentEvents(producer.Stream(cancellationToken), eventType: "ride-telemetry")`, where the producer is an `async IAsyncEnumerable<RideTelemetry>` that loops: if the ride is running, `yield return _store.GetTelemetry()`; then `await Task.Delay(telemetryInterval, timeProvider, ct)`. The endpoint stays a thin dispatcher (ADR-0007) — the loop lives in a small injectable producer (e.g. `RideTelemetryStream`) in the module's `Application` layer so it is unit-testable without HTTP.

- *Alternative — write raw `text/event-stream` by hand:* unnecessary now that `TypedResults.ServerSentEvents` exists and handles framing + JSON serialization. Rejected.
- *Alternative — `IAsyncEnumerable<SseItem<T>>`:* only needed to set per-event `id`/`event` fields; a single `eventType` argument covers our one event type. Keep the simpler `IAsyncEnumerable<T>` overload.

### 2. Per-connection sampling of the in-memory store (no broadcaster)

Each connection samples `IRideStore.GetTelemetry()` on its own `PeriodicTimer`/`Task.Delay` cadence. `GetTelemetry()` returns an immutable snapshot from the single-writer store, so concurrent readers are safe and there is no shared mutable stream state. With one ride and a handful of operator screens, building a snapshot at 30 Hz per client is negligible.

- *Alternative — a single background broadcaster + `Channel`/fan-out:* compute once, push to N subscribers. More moving parts (subscriber registry, backpressure) for no benefit at this scale. Revisit only if client count grows. Noted, not built.

Timing uses the injected `TimeProvider` (as `RideSimulationService` already does) so tests are deterministic.

### 3. Telemetry rate is a new `RideParameters` value, decoupled from `dt`

Introduce e.g. `RideParameters.TelemetryInterval` (default 30 Hz → `TimeSpan.FromSeconds(1/30.0)`), separate from `RideParameters.TimeStep` (1/120 s). This honours the accumulator model in the physics docs: physics integrates at 120 Hz; the stream samples at 30 Hz. The rate is a single named constant so it is easy to tune.

### 4. Emission gated on the running state

The producer yields a frame only when the ride is in its running state (the `Started` state introduced by `add-ride-state-machine`; today's `Running`). While not running it emits nothing but keeps awaiting, so the HTTP connection stays open across idle periods and the next run resumes streaming with no client reconnect. To keep intermediaries from dropping a long-idle connection, the producer MAY emit a lightweight heartbeat (an SSE comment) on a slow cadence; treated as optional hardening, not required by the spec.

- *Cross-change coupling:* this depends on the state machine's running state. To avoid a hard ordering constraint, gate on a small predicate (`ride.IsRunning`) rather than a specific enum name, so it works whether the running state is called `Running` or `Started`.

### 5. Frontend: `SseRideTelemetrySource` implements the existing seam; store and panels unchanged

Implement `RideTelemetrySource` with an SSE-backed source that (a) opens a native `EventSource('/api/ride/telemetry/stream')` in its constructor, (b) on each `message` parses the JSON `RideTelemetry` DTO, maps it to the frontend model, and `set`s the `telemetry` signal, and (c) implements `applyCommand` by POSTing to the existing command endpoints (`/api/ride/main-power`, `/hub-power`, `/brake`, …). Because `RideStateService` already projects `source.telemetry` into per-panel signals, **no panel changes** are needed — swapping the provider re-points the whole dashboard at the backend. `EventSource` gives auto-reconnect for free; the source unsubscribes/closes via `DestroyRef`.

- The source seeds its signal with a defined **at-rest snapshot** so panels render before the first frame and while the ride is idle (no frames flowing).
- *Alternative — a brand-new store separate from `RideStateService`:* redundant; `RideStateService` is already the single store the panels subscribe to. Reuse it.

### 6. Provider swap; simulator retained for tests/offline

`app.config.ts` changes `{ provide: RIDE_TELEMETRY_SOURCE, useExisting: SimulatedRideTelemetrySource }` to the SSE source. The simulator and `fake-ride-telemetry-source` remain for unit/spec tests and offline development. This is the **breaking** frontend behavior change: the dashboard now needs the backend running.

### 7. Backend→frontend model mapping

Map the backend `RideTelemetry` DTO (PascalCase; `Hubs[].PowerWatts/Rpm`, `Gondolas[].AngleDegrees/Rpm/ForwardG/LateralG/LoadKg/Brake/Seats[]`, `Mill.*`) to the frontend `RideTelemetry` model (`mill/hubs/gondolas/gondolaBrakeEngaged`). Where the visualization needs a field the current model lacks (notably gondola **angle** for rebuilding geometry, per docs §1.2), extend the frontend model minimally. Power/direction: the backend reports power in watts and has no per-motor "direction" concept the UI shows as forward/reverse — map power to a 0–100 percentage using `RideParameters`/max-power, and derive or default direction; capture any lossy mapping in the mapping helper with a comment.

### 8. Dev-server proxy must not buffer SSE

The Angular dev server proxies `/api` to the backend. Ensure the proxy passes `text/event-stream` through without buffering (SSE works over the existing http-proxy as long as compression/buffering isn't applied to the stream). Verify `proxy.conf` and disable any response buffering for the stream path.

## Risks / Trade-offs

- **[Proxy or reverse-proxy buffers the stream]** → frames arrive in bursts or never. Mitigation: verify the dev proxy forwards `text/event-stream` unbuffered; document the same for any production reverse proxy (disable buffering, no response compression on the stream).
- **[Dashboard now hard-depends on the backend]** (breaking) → with the backend down the panels show only the at-rest seed. Mitigation: keep the simulator wired for tests/offline and document how to switch back; the seed snapshot keeps the UI functional-looking rather than blank.
- **[Idle connections dropped by intermediaries]** since no frames flow when the ride is stopped → client reconnect churn. Mitigation: `EventSource` auto-reconnects; optionally emit a slow SSE-comment heartbeat while idle.
- **[Per-connection sampling doesn't scale to many clients]** → N× snapshot cost. Accepted at current scale; the producer is isolated so a shared broadcaster can replace it later without touching the endpoint or client.
- **[Lossy power/direction mapping]** watts→percent and absent direction → UI values may not match the simulator's semantics exactly. Mitigation: centralize the mapping in one helper with tests; extend the telemetry contract later if the UI needs true commanded values.
- **[Ordering vs `add-ride-state-machine`]** the running-state gate references that change. Mitigation: gate on an `IsRunning` predicate, not a literal enum name; if applied first, bind it to today's `Running` state.

## Migration Plan

1. Backend: add `RideParameters.TelemetryInterval`, the `RideTelemetryStream` producer, and the `GET /ride/telemetry/stream` endpoint; unit-test the producer (emits while running, quiet while stopped, honours cancellation). `dotnet build`/`dotnet test` green.
2. Frontend: add `SseRideTelemetrySource` + mapping helper (+ minimal model extension), seed at-rest snapshot, wire commands to HTTP endpoints, add specs with a fake `EventSource`. Swap the provider in `app.config.ts`. `npm test` green.
3. Verify end-to-end against a running backend: start the ride, confirm live frames drive every panel and cease when stopped, and confirm reconnect after a dropped connection.
4. Rollback: re-point `RIDE_TELEMETRY_SOURCE` back to `SimulatedRideTelemetrySource`; the backend endpoint is additive and can stay.

## Open Questions

- Final telemetry rate — 30 Hz (docs' example) vs a lower rate to cut network chatter? Default 30 Hz, revisit after seeing real payload sizes.
- Should the frontend model gain the full angle arrays (mill/hub/cart) to enable the true 3-D geometry rebuild described in docs §1.2, or is per-gondola angle enough for the current visualization? Start with what the current viz needs; expand if/when the 3-D view lands.
