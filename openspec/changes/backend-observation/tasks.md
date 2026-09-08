## 1. Shared instruments and conventions (Core)

- [x] 1.1 Add the domain instruments to `Shared/Core/Observability/BoogaBoosterTelemetry.cs` alongside the two existing handler instruments: `boogabooster.ride.simulation.ticks` (counter), `boogabooster.ride.simulation.tick.duration` (histogram, ms), `boogabooster.ride.passengers.boarded` (counter), `boogabooster.ride.state.transitions` (counter, tagged by outcome and target state per design open question), `boogabooster.queue.groups.queued` (counter), `boogabooster.queue.people.queued` (counter), `boogabooster.weather.disturbances` (counter, tagged by kind), `boogabooster.messaging.events.published` (counter, tagged by topic and outcome).
- [x] 1.2 Confirm no `Meter` or `ActivitySource` is created anywhere but `BoogaBoosterTelemetry` (grep `new Meter(` / `new ActivitySource(` across `src/`) — spec requirement "All metrics share one meter".
- [x] 1.3 Verify `ServiceDefaults` needs no edit: it already registers `BoogaBoosterTelemetry.SourceName` as both an `AddSource` and an `AddMeter`, so every new instrument is collected without change (design non-goal — do not touch this file).
- [x] 1.4 Add `MeterListener`-based assertions for the new instruments to `Tests/FourDotnet.BoogaBooster.Core.Tests/HandlerInstrumentationTests.cs` or a sibling class, following the listener pattern already in that file (built-in `MeterListener`, not `MetricCollector` — no new package).

## 2. Integration-event publisher (IntegrationMessages)

- [x] 2.1 Instrument `DaprIntegrationEventPublisher.PublishAsync` with an activity from `BoogaBoosterTelemetry.ActivitySource`, `ActivityKind.Producer`, tagged `messaging.system` = `dapr`, `messaging.destination.name` = the resolved topic, `messaging.event.type` = the runtime event type name, and the pub/sub component name.
- [x] 2.2 Record error status and `AddException` when the transport throws, then rethrow — the publisher must not swallow.
- [x] 2.3 Record `boogabooster.messaging.events.published` tagged by topic and outcome (`ok`/`error`) — both bounded sets, no event id.
- [x] 2.4 Add tests to `Tests/FourDotnet.BoogaBooster.IntegrationMessages.Tests/` asserting the topic/type/system tags on success, error status plus the `error`-outcome counter on transport failure, and that the publish activity is a child of an ambient parent activity (design D7).

## 3. Weather module

- [x] 3.1 Declare the Weather attribute-name constants in one place in the module (`weather.regime`, `weather.nice_weather`, `weather.precipitation.type`, `weather.wind.speed_ms`, `weather.disturbance.kind`) so no call site repeats a literal.
- [x] 3.2 Override `EnrichActivity` on `StartPrecipitationCommandHandler` to record the requested precipitation type and the regime in effect when the command arrived.
- [x] 3.3 Override `EnrichActivity` on `StartStrongWindCommandHandler` to record the regime and wind speed the command found — the command is payload-free, so this is what makes one invocation distinguishable from another (design D2).
- [x] 3.4 Override `EnrichActivityWithResponse` on `GetWeatherQueryHandler` to record the regime and the nice-weather reading from the returned `WeatherConditionDto`.
- [x] 3.5 Record `boogabooster.weather.disturbances` from both disturbance handlers, tagged by kind (`precipitation`/`strong-wind`).
- [x] 3.6 Instrument `WeatherSimulationService`: one activity per advance, tagged with the resulting regime and nice-weather reading; error status on a failing pass.
- [x] 3.7 Add `WeatherHandlerTelemetryTests` to `Tests/FourDotnet.BoogaBooster.Weather.Tests/` following the `QueueHandlerTelemetryTests` `ActivityListener` pattern, covering all three handlers and the simulation service.

## 4. Queue module (handlers already done)

- [x] 4.1 Instrument `RideQueueFillerService`: one activity per fill pass, tagged with the ride filled, groups added and people added; error status on a failing pass.
- [x] 4.2 Record `boogabooster.queue.groups.queued` and `boogabooster.queue.people.queued` where groups are actually enqueued, tagged only with bounded values (no ride id, no group id — design D6).
- [x] 4.3 Extend `Tests/FourDotnet.BoogaBooster.Queue.Tests/QueueHandlerTelemetryTests.cs` (or add a sibling `QueueBackgroundTelemetryTests`) to cover the filler service's span and counters.
- [x] 4.4 Confirm the two existing Queue handler overrides still satisfy the new spec — in particular that `GetQueueStatus` records counts and no individual queued person reaches a span.

## 5. DigitalTwin module — handlers

- [x] 5.1 Declare the DigitalTwin attribute-name constants in one place in the module (`ride.state`, `ride.state.requested`, `ride.hub.index`, `ride.gondola.index`, `ride.seat`, `ride.engine`, `ride.engine.power_percent`, `ride.engine.direction`, `ride.brake.engaged`, `ride.gondola.brake`, `ride.passengers.boarded`, `ride.mill.rpm`).
- [x] 5.2 Override `EnrichActivity` on `SetMainEnginePowerCommandHandler` and `SetHubEnginePowerCommandHandler` to record the commanded percentage and which drive was commanded, so the two spans are distinguishable.
- [x] 5.3 Override `EnrichActivity` on `SetMainEngineDirectionCommandHandler` and `SetHubEngineDirectionCommandHandler` to record the commanded direction and which drive.
- [x] 5.4 Override `EnrichActivity` on `BoardPassengerCommandHandler` to record hub index, gondola index, seat and whether a weight was supplied — **not** the weight value and never a name (design "no personal data").
- [x] 5.5 Override `EnrichActivity` on `SetGondolaBrakeCommandHandler` to record hub index, gondola index and the requested brake state.
- [x] 5.6 Override `EnrichActivity` on `BrakeEnginesCommandHandler` to record the requested engaged/released state and the state the brake was already in, so a no-op is visible as one.
- [x] 5.7 Override `EnrichActivity` on `RequestRideStateTransitionCommandHandler` to record the requested target state and the state the ride was in — these must survive on the span when the guard rejects the request (spec: "A rejected transition is traceable").
- [x] 5.8 Override `EnrichActivity` on `StartRideCommandHandler` and `StopRideCommandHandler` to record the lifecycle state the command found (payload-free commands, design D2).
- [x] 5.9 Override `EnrichActivityWithResponse` on `GetRideTelemetryQueryHandler` to record the lifecycle state, boarded passenger count and mill RPM from the snapshot returned — summary scalars only, not the 16-gondola payload.
- [x] 5.10 Record `boogabooster.ride.passengers.boarded` from the boarding handler and `boogabooster.ride.state.transitions` (tagged by outcome, and by target state) from the transition handler, including on rejection.

## 6. DigitalTwin module — background work

- [x] 6.1 Instrument `RideSimulationService` with the tick counter and tick-duration histogram, and **no activity per tick** (design D4 — 120 Hz). Keep the instrumentation out of any path that feeds simulation state, so the tick stays a pure function of state at fixed `dt` (`docs/01 §1.1`).
- [x] 6.2 Instrument `RideLoadingCoordinator.RunLoadingPassAsync` to start an activity only when a pass actually boards a group, tagged with the ride addressed and the number of passengers boarded; leave the signature `Task`-returning (design D5).
- [x] 6.3 Give the loading pass's failure path error status on its activity, so the existing log-and-continue in `RideSimulationService.TickAsync` no longer hides a broken pass (spec: "A background failure is recorded, not swallowed silently").
- [x] 6.4 Add `DigitalTwinHandlerTelemetryTests` to `Tests/FourDotnet.BoogaBooster.DigitalTwin.Tests/` covering all 11 handler overrides, asserting against the shared name constants.
- [x] 6.5 Add tests asserting the simulation loop emits the tick counter and duration histogram and starts **no** activity per tick, and that a boarding loading pass does start one.
- [x] 6.6 Add a test asserting no passenger name appears on any attribute of a boarding span (spec: "No personal data reaches a span").

## 7. Verification and guidance

- [x] 7.1 Run `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` from `src/` — all green. **Build clean (0 errors); all 5 test projects green — 469 tests, 0 failures.** `dotnet test` itself cannot run on this machine: the .NET 10 SDK no longer supports the VSTest target for Microsoft.Testing.Platform projects, which is a pre-existing tooling issue unrelated to this change (a `dotnet.config` MTP opt-in did not take). The xunit.v3 projects are self-executing, so each was run directly from `bin/Debug/net10.0/`.
- [x] 7.2 Verify every command and query handler now has at least one telemetry test (spec: "Every handler has a telemetry test"); a quick way is to assert the count of `EnrichActivity` overrides matches the count of handlers.
- [x] 7.3 Audit every metric introduced for tag cardinality: confirm no metric is tagged with a ride id, group id, gondola index or passenger weight (design D6).
- [x] 7.4 Confirm module libraries and `Shared/` libraries still hold ≥ 80 % line coverage (the `test-coverage` skill owns this floor). **All clear:** Core 99.4 %, IntegrationMessages 100 %, Queue 94.7 %, DigitalTwin 86.1 %, Weather 85.2 % (line rate per module library, measured with `dotnet-coverage` against each library's own test project).
- [ ] 7.5 Run the ride via the AppHost and confirm in the Aspire dashboard that handler spans carry their new attributes, the publish span nests under its handler, and the new metrics appear — the one check the unit tests cannot make. **Not done — needs the user.** It requires Docker, an initialised Dapr CLI and the RabbitMQ user-secrets, and it is an interactive dashboard check by design.
- [x] 7.6 Write the instrumentation convention down (attribute prefixes, span-vs-metric cardinality rule, the "instrument background work" expectation) as a skill under `.claude/skills/` or a section in `CLAUDE.md`; if it lands in `CLAUDE.md`, mirror it into `.github/copilot-instructions.md` as that file's rules require.
- [x] 7.7 Resolve the two design open questions in code and note the outcome: whether the transition counter tags target state, and whether the loading pass records why it boarded nobody.
