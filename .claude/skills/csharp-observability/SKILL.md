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

```csharp
public sealed class QueueGroupCommandHandler(IQueueStore store)
    : CommandHandler<QueueGroupCommand>
{
    public override async Task HandleAsync(QueueGroupCommand command, CancellationToken ct)
    {
        using var activity = Telemetry.ActivitySource.StartActivity("QueueGroup");
        activity?.SetTag("queue.group.size", command.Size);

        try
        {
            await store.EnqueueAsync(command, ct);
            Telemetry.GroupsQueued.Add(1, new KeyValuePair<string, object?>("outcome", "ok"));
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            Telemetry.GroupsQueued.Add(1, new KeyValuePair<string, object?>("outcome", "error"));
            throw;
        }
    }
}
```

Where the base class already handles the activity, timing and outcome counters,
the concrete handler adds only its own tags and domain metrics — do not duplicate
the plumbing.

## Violations to catch

- A command or query handler with no activity and no metrics.
- `AddOpenTelemetry()` / exporter configuration inside a module or an API host
  instead of `ServiceDefaults`.
- A project that does not call `AddServiceDefaults()`.
- High-cardinality metric tags (entity ids, user ids, free-text).
- A span tag carrying a secret, token, or personal data.
- A second logging/metrics stack (Serilog sinks to a separate backend, App
  Insights SDK alongside OTEL, etc.).
- A new background service or integration added without instrumentation.

## Read further

`get_document` with `adr-0009` on the `4dotnet-csharp-style-guide` MCP server for
the full list of what the central configuration must enable and the enforcement
notes. `adr-0008` covers using each Aspire integration's client library, which is
what brings its instrumentation along.
