---
name: csharp-minimal-api-endpoints
description: >-
  HTTP endpoint rules for the .NET backend (ADR-0002, ADR-0007). Use whenever
  adding, changing, moving or reviewing an HTTP endpoint, route, route group or a
  module's HTTP surface, and whenever wiring a module into a host. Minimal APIs
  only — controllers are prohibited. Endpoint mappings live in the owning
  module's Endpoints/ namespace, never in the API project, and every module
  exposes exactly Add<Module>Module() and Map<Module>Endpoints().
---

# Minimal-API Endpoints

Endpoints belong to the **module**, not the host. The API project is a thin
composition root that calls two extension methods per module and nothing else.

## Non-negotiable

- **Minimal APIs only.** Never add a controller or a controller-based endpoint.
  (`adr-0002-r1`, `adr-0002-r2`)
- **All endpoint mappings live in the module library**, in an `Endpoints`
  namespace. The API project contains **no** endpoint mappings — never add one to
  `Api/Program.cs`. (`adr-0007-r1`)
- **Exactly two public extension methods per module** (`adr-0007-r2`):
  - `Add<Module>Module()` on `IHostApplicationBuilder` — registers everything the
    module needs, including its feature handlers.
  - `Map<Module>Endpoints()` on the web app — maps every endpoint the module owns.
- **Endpoints contain no business logic.** Parse and validate the request into a
  command or query, dispatch it to an injected `ICommandHandler<T>` /
  `IQueryHandler<T,R>`, shape the result into an HTTP response. (`adr-0005-r2`)
- **Use `MapGroup` pragmatically, not dogmatically** — group where it genuinely
  removes repetition of prefix, tags, filters or metadata; do not force a group
  that adds ceremony for a single route. (`adr-0007`)
- **Request and response bodies are DTOs from the module's `.Abstractions`
  project** — see the `dto-organization` skill. Never declare a request record
  inline in the endpoints class.
- **`DomainValidationException` translates to `400`.** Let broken invariants
  surface from the domain instead of re-checking business rules in the endpoint.

## The shape

```csharp
namespace FourDotnet.BoogaBooster.Weather.Endpoints;

public static class WeatherEndpoints
{
    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/weather").WithTags("Weather");

        group.MapGet("/", async (IQueryHandler<GetWeatherQuery, WeatherConditionDto> handler,
                                 CancellationToken ct)
            => Results.Ok(await handler.HandleAsync(new GetWeatherQuery(), ct)));

        group.MapPost("/precipitation", async (StartPrecipitationRequest request,
                                               ICommandHandler<StartPrecipitationCommand> handler,
                                               CancellationToken ct) =>
        {
            await handler.HandleAsync(new StartPrecipitationCommand(request.Intensity), ct);
            return Results.Accepted();
        });

        return app;
    }
}
```

The host then reads:

```csharp
builder.AddWeatherModule();
// ...
app.MapWeatherEndpoints();
```

## Pub/sub subscriptions

Dapr subscriptions are plain minimal-API endpoints annotated `.WithTopic(...)`
and discovered by `MapSubscribeHandler()`. They follow the same rules: they live
in the owning module's `Endpoints/`, and they dispatch to a handler. Read
`src/Shared/FourDotnet.BoogaBooster.IntegrationMessages/README.md` before adding
an event.

## Violations to catch

- Any endpoint mapping in `FourDotnet.BoogaBooster.Api`.
- A controller class, or an `AddControllers()` / `MapControllers()` call.
- An endpoint configuration outside the module's `Endpoints` namespace.
- A module missing either extension method, or exposing more than those two as
  its host-facing surface.
- Validation, orchestration or persistence code inside a lambda in `Endpoints/`.
- A request record declared inline in the endpoints class.

## Read further

`get_document` on the `4dotnet-csharp-style-guide` MCP server: `adr-0002`
(minimal APIs) and `adr-0007` (module-owned endpoints, portability rationale).
