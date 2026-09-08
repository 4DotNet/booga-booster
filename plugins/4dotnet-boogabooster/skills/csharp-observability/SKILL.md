---
name: csharp-observability
description: >-
  OpenTelemetry instrumentation rules for the .NET backend (ADR-0009). Use
  whenever writing or changing a command or query handler, adding a background
  service or integration, touching ServiceDefaults or any telemetry/logging/
  metrics configuration, or reviewing whether a change is observable. Tracing and
  metrics are required, not optional: OTEL is configured in exactly one place
  (Aspire ServiceDefaults), and every handler starts an activity, tags it,
  records failure status and emits counter/histogram metrics.
---

# Observability (OpenTelemetry)

A handler with no tracing or metrics is **incomplete**, the same way an untested
handler is incomplete. Instrumentation is part of the change, not a follow-up.

## Non-negotiable

- **Traces and metrics are required on every change**, not an optional add-on.
  (`adr-0009-r1`)
- **OTEL is configured in exactly one place.** This solution uses Aspire, so that
  place is `Aspire/FourDotnet.BoogaBooster.Aspire.ServiceDefaults`, and every
  project calls `builder.AddServiceDefaults()`. Extend that project when you need
  extra instrumentation — **never** configure OTEL ad hoc in a module or host.
  (`adr-0009-r2`)
- **Every command and query handler starts an activity** from the shared
  `ActivitySource`, named after the operation (`GetWeather`, `QueueGroup`).
  (`adr-0009-r3`)
- **Tag the span with what matters for diagnosis** — entity ids, counts, the
  inputs you would want during an incident. (`adr-0009-r3`)
- **Record the outcome**: on failure set the activity status to error and call
  `AddException`. Success completes OK/unset. (`adr-0009-r3`)
- **Emit metrics through the shared `Meter`**: a counter for invocations and
  failures, a histogram for duration or a domain-meaningful measure. Use
  **low-cardinality tags** only (operation name, outcome). (`adr-0009-r4`)
- **Put the common plumbing in the CQRS base classes** — starting the activity,
  timing, success/failure counters — so each concrete handler only adds its own
  attributes and domain metrics. (`adr-0009-r4`)
- **Never put secrets or personal data on a span**, and never add a competing
  observability stack alongside OpenTelemetry. (`adr-0009-r5`)

## What the central configuration must provide

The `ServiceDefaults` project must set up, at minimum:

- **Resource attributes** — service name, version, environment on every signal.
- **Tracing** — ASP.NET Core, `HttpClient`, and the client instrumentation that
  ships with each Aspire integration (messaging, cache, database).
- **Metrics** — ASP.NET Core, `HttpClient`, runtime.
- **Our own `ActivitySource` and `Meter`** — one well-known name per service (or
  per module), registered so they are collected and exported.
- **OTLP export**, with logs going through the OpenTelemetry logging provider so
  they carry trace correlation.

## The shape

The base classes in `FourDotnet.BoogaBooster.Core.Cqrs` already start the
activity, time the invocation, record error status plus `AddException`, and emit
`boogabooster.handler.invocations` and `boogabooster.handler.duration`. **Do not
duplicate any of that.** A handler implements `ExecuteAsync` and overrides
`EnrichActivity` — the hook the base calls only when something is listening:

```csharp
public sealed class SetMainEnginePowerCommandHandler : CommandHandler<SetMainEnginePowerCommand>
{
    private readonly IRideStore _store;

    public SetMainEnginePowerCommandHandler(IRideStore store) => _store = store;

    protected override Task ExecuteAsync(SetMainEnginePowerCommand command, CancellationToken ct)
    {
        _store.SetMainEnginePower(new EnginePower(command.Percent));
        return Task.CompletedTask;
    }

    protected override void EnrichActivity(Activity activity, SetMainEnginePowerCommand command)
    {
        activity.SetTag(RideTelemetryAttributes.Engine, RideTelemetryAttributes.MainEngine);
        activity.SetTag(RideTelemetryAttributes.EnginePowerPercent, command.Percent);
    }
}
```

A query handler overrides `EnrichActivityWithResponse` as well, to describe what
came back — counts, sizes and scalar readings, never the payload itself.

## This repo's instrumentation convention

### Every handler tags its own span — no exceptions

A handler whose command carries **no payload** still records the state its work
depended on: the ride's lifecycle state as it found it, the weather regime in
force. That is what makes one `StartRide` span distinguishable from another
during an incident, and it is why an empty `EnrichActivity` is a violation rather
than an acceptable default.

Set the attributes **before** calling into the domain, so they survive on the
span when a guard refuses the request — a rejected transition that says only
"error" is useless.

Each module test project holds a reflection-driven gate
(`<Module>HandlerInstrumentationGateTests`) asserting every handler declares an
`Enrich*` override, so a handler that stops tagging fails the build.

### Attribute names: `<module>.<subject>.<detail>`

Dot-delimited, lower-case, snake_case within a segment, prefixed with the owning
module: `ride.`, `queue.`, `weather.`, `messaging.`. Metric names are prefixed
`boogabooster.`.

Names are declared as `const string` in **one place per module** —
`<Module>/Observability/<Module>TelemetryAttributes.cs` — never repeated as a
literal at each call site, so a rename is a single edit and a test cannot be left
asserting a dead string.

**This applies to every call site, not just handlers.** A background service, a
coordinator or a publisher adds its names to the module's registry too; a local
`private const` beside the code that uses it is the same defect as a literal —
it is outside the registry a rename would touch.

The genuinely shared names — the base-class span and metric tags, and the
`outcome` key every success/failure instrument uses — live in
`Core/Observability/TelemetryTags.cs`, with their values in `TelemetryOutcome`.
Module vocabulary stays in the module: `Core` must not accumulate it.

Follow the OpenTelemetry **messaging** semantic conventions where they apply
(`messaging.system`, `messaging.destination.name`) — there is no semantic
convention for a carnival-ride digital twin, so domain attributes get our own
prefix rather than an invented `otel.*`-adjacent name.

### Identifiers live on spans; metric tags stay bounded

This is the rule that keeps the metrics affordable:

| | Span attribute | Metric tag |
| --- | --- | --- |
| Ride id, group id | ✅ | ❌ unbounded |
| Gondola index, hub index, seat | ✅ | ❌ 16× / 32× every series |
| Passenger weight | ❌ personal data | ❌ continuous |
| Lifecycle state, precipitation type, topic, outcome, operation | ✅ | ✅ fixed set |

A span is already one record per operation, so a high-cardinality attribute costs
nothing there. A metric tag multiplies the time series. **A metric tag must take
its value from a fixed, enumerable set** — and if a counter has nothing bounded
worth tagging, record it untagged.

### No personal data, by construction

The boarding span records hub, gondola, seat and **whether** a weight was
supplied — not the weight, and never a name. The queue records counts, not the
people waiting. Both are asserted absent by test, not left to review.

### Background work is instrumented at a cadence that suits it

- **Work that acts intermittently spans each pass** — the queue filler, the
  weather advance. Span the passes that do nothing too: "the line is not growing"
  is the question an operator actually asks.
- **Work at the physics tick rate is measured, never spanned.** The 120 Hz
  simulation loop gets a counter and a duration histogram and **no activity per
  tick**: 7,200 near-identical spans a minute would swamp the backend and bury
  the one long tick worth seeing. A pass that is genuinely noteworthy — the
  loading pass that actually boards a group — starts its activity lazily, so the
  common no-op case stays free.
- **A pass that throws gets error status on its own span**, even where the caller
  logs and continues. A `catch`-and-log with no span is how a broken background
  pass stays invisible.
- **Instrumentation must not feed back into simulation state.** In the tick, the
  elapsed time is read *after* the advance and handed only to the histogram; no
  clock reading enters the step, which stays a pure function of state at fixed
  `dt` (`docs/01 §1.1`).

### Publishing is a child span of the work that caused it

`DaprIntegrationEventPublisher` spans each publish from the same shared
`ActivitySource`, so `Activity.Current` makes it a child of the ambient handler
activity automatically. That nesting is the end-to-end trace ADR-0009 is after —
never start a publish span from a source of its own.

### Testing it

Capture activities with an `ActivityListener` scoped to
`BoogaBoosterTelemetry.SourceName` and metrics with the built-in `MeterListener`
(**not** `MetricCollector<T>` — it would pull in a test package for no gain).
Each module test project has a `TelemetryRecorder` doing both.

The source and meter are **process-wide**, so a listener also sees what test
classes running in parallel emit. Isolate activities by opening a scope
(`_telemetry.Scope()`) and filtering on its trace id; measurements carry no trace
id, so assert that a matching measurement *exists* rather than that it is the
only one, and discriminate on a value no sibling test uses.

Assert against the same name constants the production code writes.

## Violations to catch

- A command or query handler with no activity and no metrics.
- A handler that inherits the empty `EnrichActivity`, including one whose command
  carries no payload — it still records the state its work depended on.
- A handler re-implementing the base-class plumbing (starting its own activity,
  timing itself, counting its own invocations) instead of overriding
  `EnrichActivity`.
- Attribute or metric-tag names written as literals at the call site instead of
  referencing the module's `<Module>TelemetryAttributes` constants — or declared
  as a `private const` next to the code that uses them, which keeps them out of
  the registry a rename would touch. Background services and publishers are held
  to this as strictly as handlers.
- `AddOpenTelemetry()` / exporter configuration inside a module or an API host
  instead of `ServiceDefaults`.
- A project that does not call `AddServiceDefaults()`.
- High-cardinality metric tags (entity ids, user ids, gondola index, a continuous
  measure, free-text).
- A `new Meter(...)` or `new ActivitySource(...)` anywhere but
  `BoogaBoosterTelemetry`.
- A span tag carrying a secret, token, or personal data — a passenger's name or
  weight, an individual queued person.
- A second logging/metrics stack (Serilog sinks to a separate backend, App
  Insights SDK alongside OTEL, etc.).
- A new background service or integration added without instrumentation.
- An activity started per tick in the physics loop, or a clock reading that feeds
  simulation state.
- A background `catch`-and-log whose pass leaves no error-status span behind.

## Read further

`get_document` with `adr-0009` on the `4dotnet-csharp-style-guide` MCP server for
the full list of what the central configuration must enable and the enforcement
notes. `adr-0008` covers using each Aspire integration's client library, which is
what brings its instrumentation along.
