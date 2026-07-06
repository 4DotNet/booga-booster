## 1. Consult the style guide

- [x] 1.1 Query the `4dotnet-csharp-style-guide` MCP server for ADR-0002 (minimal APIs), ADR-0007 (endpoints in module), ADR-0004 (module/abstractions boundary), and the unit-testing guideline before writing any C#.

## 2. Backend: telemetry rate and stream producer

- [x] 2.1 Add `RideParameters.TelemetryInterval` (default 30 Hz, e.g. `TimeSpan.FromSeconds(1.0 / 30)`), documented as decoupled from the physics `TimeStep` (1/120 s).
- [x] 2.2 Add an `IsRunning` predicate to the ride/store (true only in the running/`Started` state) so emission can be gated without hard-coding an enum name.
- [x] 2.3 Create `Application/RideTelemetryStream.cs`: an injectable producer with `IAsyncEnumerable<RideTelemetry> Stream([EnumeratorCancellation] CancellationToken ct)` that loops — when the ride is running `yield return _store.GetTelemetry()`, then `await Task.Delay(RideParameters.TelemetryInterval, _timeProvider, ct)`; skip yielding while not running; exit cleanly on cancellation. Inject `IRideStore` and `TimeProvider`.
- [ ] 2.4 (Optional hardening) emit a slow SSE-comment heartbeat while the ride is idle to keep intermediaries from dropping the connection.
- [x] 2.5 Register `RideTelemetryStream` in `DigitalTwinModuleExtensions`.

## 3. Backend: SSE endpoint

- [x] 3.1 Add `GET /ride/telemetry/stream` to `DigitalTwinEndpoints`: resolve `RideTelemetryStream`, return `TypedResults.ServerSentEvents(stream.Stream(ct), eventType: "ride-telemetry")`, taking the request `CancellationToken`. Name it (`WithName`) and keep it a thin dispatcher.
- [x] 3.2 Confirm the app's JSON options serialize `RideTelemetry` (and its enums) the way the frontend expects; align enum serialization (string vs number) with the existing telemetry endpoint.
- [x] 3.3 Keep the existing `GET /ride/telemetry` one-shot endpoint unchanged.

## 4. Backend tests (DigitalTwin.Tests, xUnit v3)

- [x] 4.1 Producer emits frames at the telemetry cadence while the ride is running (drive a fake `TimeProvider`, assert frame count/timing).
- [x] 4.2 Producer emits no frames while the ride is not running, and resumes emitting once it enters the running state.
- [x] 4.3 Producer completes/stops promptly when the cancellation token is cancelled (client disconnect).
- [x] 4.4 Each emitted frame is a full `RideTelemetry` (mill + all hubs + all gondolas + seats populated).
- [x] 4.5 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` green; module coverage stays ≥ 80%.

## 5. Frontend: SSE telemetry source

- [ ] 5.1 Add a backend→frontend mapping helper: `RideTelemetryDto` → the frontend `RideTelemetry` model (map `Mill`, `Hubs[].PowerWatts/Rpm`, `Gondolas[].Brake/AngleDegrees/Rpm/ForwardG/LateralG/LoadKg/Seats[]`; map watts→0–100 percent; derive/default direction; centralize any lossy mapping with comments).
- [ ] 5.2 Extend the frontend `ride.models.ts` minimally where the visualization needs a field the model lacks (e.g. gondola `angleDegrees`).
- [ ] 5.3 Create `ride-dashboard/data/sse-ride-telemetry-source.ts` implementing `RideTelemetrySource`: seed the `telemetry` signal with a defined at-rest snapshot; open `new EventSource('/api/ride/telemetry/stream')`; on `message`, parse + map + `set` the signal; close on `DestroyRef`. Rely on `EventSource` auto-reconnect.
- [ ] 5.4 Implement `applyCommand` by POSTing to the existing HTTP command endpoints (`/api/ride/main-power`, `/hub-power`, brake, direction) via `HttpClient`; do not mutate local state directly (the effect returns on the next frame).

## 6. Frontend: wire up and proxy

- [ ] 6.1 Swap the provider in `app.config.ts`: `RIDE_TELEMETRY_SOURCE` → `SseRideTelemetrySource`; keep `SimulatedRideTelemetrySource`/`fake-ride-telemetry-source` for tests/offline.
- [ ] 6.2 Verify/adjust the dev-server proxy so `/api/**` `text/event-stream` responses are forwarded unbuffered and uncompressed.
- [ ] 6.3 Confirm `RideStateService` and all panels work unchanged against the swapped source (they only read `source.telemetry`).

## 7. Frontend tests

- [ ] 7.1 Unit-test the mapping helper: a representative backend DTO maps to the expected frontend model (power percent, gondola/seat fields, angle).
- [ ] 7.2 Spec the SSE source with a fake `EventSource`: an incoming frame updates the `telemetry` signal; the at-rest seed is exposed before any frame; `applyCommand` issues the expected POST.
- [ ] 7.3 `npm test` (vitest) green; dashboard a11y specs still pass.

## 8. Verify end-to-end

- [ ] 8.1 Run the backend via Aspire and the Angular app; confirm the dashboard opens the SSE connection on load even while the ride is idle (no frames), then streams live frames once the ride is running, driving every panel.
- [ ] 8.2 Change engine power / toggle a brake and confirm the command POSTs and the effect appears on subsequent streamed frames.
- [ ] 8.3 Stop the ride and confirm frames cease while the connection stays open; drop the connection and confirm automatic reconnect resumes streaming on the next run.
