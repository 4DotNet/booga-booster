## Why

ADR-0009 requires that every command and query handler *"tag the span with meaningful
attributes — the identifiers and inputs that matter for diagnosis"*, and that *"new
integrations, and new background work must be instrumented as part of the change"*. The
shared CQRS base classes already own the plumbing the ADR asks for — they start the
activity, tag the operation and kind, time the invocation, record failure status, and emit
the invocation counter and duration histogram — but the per-handler half of the contract
is unmet almost everywhere.

An audit of the backend against ADR-0009 found **14 of 16 handlers add no attributes of
their own**, and **none of the three background services nor the integration-event
publisher emit a trace or a metric at all**. The result is a system whose most
interesting work is invisible: the ride simulation ticks 120 times a second and never
reports its rate, and an operator command lands on a span that records neither which
engine was commanded nor to what power. That is precisely the "handler with no tracing or
metrics is incomplete" state the ADR names as grounds for rejection at review.

## What Changes

- **Every feature handler adds its own span attributes.** The 11 DigitalTwin handlers and
  3 Weather handlers override `EnrichActivity` (and `EnrichActivityWithResponse` on the
  two queries) to record the identifiers and inputs worth having during an incident — the
  hub/gondola/seat a command addresses, the commanded power or direction, the requested
  and resulting ride state, the precipitation type, and, on the queries, the shape of what
  was read.
- **The three background services are instrumented.** `RideSimulationService`,
  `RideQueueFillerService` and `WeatherSimulationService` each report their own work
  through the shared `ActivitySource`/`Meter`, at a cadence that suits it: the 120 Hz
  physics loop is measured with counters and histograms rather than a span per tick,
  while the queue filler and weather simulation — which act intermittently — span each
  pass.
- **The integration-event publisher is instrumented.** `DaprIntegrationEventPublisher`
  spans each publish, tags the topic, records failure status, and counts published events
  by topic and outcome, so a message that never left the process is visible.
- **Domain metrics reach the shared `Meter`.** Beyond the base-class handler instruments,
  the modules publish the domain measures that make the twin legible — simulation tick
  rate and duration, passengers boarded, ride-state transitions (accepted and rejected),
  weather disturbances raised, and groups queued.
- **Guidance is written down.** A skill-level convention for attribute naming and
  cardinality, so the next handler is instrumented by default rather than by audit.

No HTTP contract, domain behaviour, physics or database shape changes. This is additive
instrumentation only: no existing endpoint, telemetry DTO or simulation result moves.

## Capabilities

### New Capabilities

- `backend-observability`: What the backend must emit to be observable per ADR-0009 — the
  span attributes every command and query handler contributes, the traces and metrics
  background services and integrations emit, the domain metrics published through the
  shared `Meter`, and the naming/cardinality rules that keep the signals aggregatable and
  free of personal data.

### Modified Capabilities

None. No existing published capability's requirements change — the handlers, endpoints and
background services keep their current observable behaviour and gain instrumentation
around it.

## Impact

**Code — instrumentation added, behaviour unchanged:**

- `DigitalTwin/Features/*` — 11 handlers gain `EnrichActivity` overrides
  (`BoardPassenger`, `BrakeEngines`, `GetRideTelemetry`, `RequestRideStateTransition`,
  `SetGondolaBrake`, `SetHubEngineDirection`, `SetHubEnginePower`,
  `SetMainEngineDirection`, `SetMainEnginePower`, `StartRide`, `StopRide`)
- `Weather/Features/*` — 3 handlers gain `EnrichActivity` overrides (`GetWeather`,
  `StartPrecipitation`, `StartStrongWind`)
- `DigitalTwin/Application/RideSimulationService.cs`,
  `Queue/Filling/RideQueueFillerService.cs`,
  `Weather/Application/WeatherSimulationService.cs` — traces and metrics added
- `Shared/IntegrationMessages/DaprIntegrationEventPublisher.cs` — span and counter per
  publish
- `Shared/Core/Observability/BoogaBoosterTelemetry.cs` — the new domain instruments and
  the shared attribute-name constants

**Not touched:** `ServiceDefaults` needs no change — the single `ActivitySource` and
`Meter` it registers (ADR-0009's one-place rule) already cover every signal added here.
The CQRS base classes need no change either; their `EnrichActivity` hooks are the
extension point this change finally uses.

**Tests:** one telemetry test class per module, following the `ActivityListener` pattern
already established in `Tests/FourDotnet.BoogaBooster.Queue.Tests/QueueHandlerTelemetryTests.cs`
and `Tests/FourDotnet.BoogaBooster.Core.Tests/HandlerInstrumentationTests.cs`, plus
`MetricCollector` coverage for the new domain instruments. The ≥ 80 % line-coverage floor
applies as usual.

**Risk:** low, and concentrated in two places — metric tag cardinality (a per-gondola tag
would multiply series 16-fold) and the 120 Hz physics loop, where a span per tick would
cost more than it reveals. Both are addressed in `design.md`.

**Dependencies:** none added. Everything needed ships in
`System.Diagnostics.DiagnosticSource`, already referenced transitively.
