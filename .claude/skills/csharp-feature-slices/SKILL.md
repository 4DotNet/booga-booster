---
name: csharp-feature-slices
description: >-
  CQRS and feature-slice organization for the .NET backend (ADR-0005, ADR-0006).
  Use whenever adding or changing a command, query, or handler, implementing a
  new backend feature or use case, deciding where application logic belongs, or
  reviewing a module's Features folder. Every endpoint is one feature living in
  {Module}.Features.{FeatureName} with its command/query and handler; base
  classes are hand-written in FourDotnet.BoogaBooster.Core.Cqrs and external
  mediator libraries (MediatR and friends) are strictly prohibited.
---

# CQRS and Feature Slices

Every operation in the backend is one **feature**: one command or query, one
handler, one folder. ADR-0006 says this organization is required and has no
exceptions.

## Non-negotiable

- **One feature per endpoint**, named after the operation in verb-noun form:
  `GetWeather`, `StartPrecipitation`, `QueueGroup`. (`adr-0006-r1`)
- **Namespace is `FourDotnet.BoogaBooster.<Module>.Features.<Feature>`**, and the
  folder mirrors it: `Features/<Feature>/`. Never group these types by technical
  layer, never put two features in one namespace. (`adr-0006-r1`)
- **Use the hand-written base classes** in `FourDotnet.BoogaBooster.Core.Cqrs`:
  `Command`, `Query<TResponse>`, `CommandHandler<TCommand>`,
  `QueryHandler<TQuery, TResponse>`. Handlers derive from the base **classes**,
  not the bare interfaces. (`adr-0005-r1`)
- **No external CQRS or mediator library.** MediatR or any equivalent is
  prohibited, with no exception without a new accepted ADR. (`adr-0005-r3`)
- **A query always has a response type. A command's response is optional.**
  (`adr-0005`)
- **The endpoint converts the DTO into the command/query** and dispatches to a
  DI-resolved handler. Business logic lives in the handler, never the endpoint.
  (`adr-0005-r2`)
- **Handlers are instrumented.** A handler with no traces or metrics is
  incomplete — see the `csharp-observability` skill. (`adr-0009-r3`)

## The shape

```
<Module>/FourDotnet.BoogaBooster.<Module>/
  Features/
    GetWeather/
      GetWeatherQuery.cs           namespace ....Weather.Features.GetWeather
      GetWeatherQueryHandler.cs
    StartPrecipitation/
      StartPrecipitationCommand.cs
      StartPrecipitationCommandHandler.cs
```

```csharp
namespace FourDotnet.BoogaBooster.Weather.Features.GetWeather;

public sealed record GetWeatherQuery : Query<WeatherConditionDto>;

public sealed class GetWeatherQueryHandler(IWeatherStore store)
    : QueryHandler<GetWeatherQuery, WeatherConditionDto>
{
    public override Task<WeatherConditionDto> HandleAsync(
        GetWeatherQuery query, CancellationToken cancellationToken) => ...;
}
```

Register every handler against its **interface** in the module's
`Add<Module>Module()` (ADR-0007) so endpoints can inject it:

```csharp
services.AddScoped<IQueryHandler<GetWeatherQuery, WeatherConditionDto>, GetWeatherQueryHandler>();
services.AddScoped<ICommandHandler<StartPrecipitationCommand>, StartPrecipitationCommandHandler>();
```

## Naming

| Type | Name |
| --- | --- |
| Command | `<Feature>Command` |
| Query | `<Feature>Query` |
| Command handler | `<Feature>CommandHandler` |
| Query handler | `<Feature>QueryHandler` |
| Request / Response DTO | `<Feature>Request` / `<Feature>Response` in `.Abstractions` — see `dto-organization` |

A feature-specific type the feature owns (a projection, a small helper) lives in
the same feature namespace. A type genuinely shared by several features is lifted
deliberately to the module or to `Shared/` — never left inside one slice for
other slices to reach into.

## Violations to catch

- A command, query or handler outside `Features/<Feature>/`.
- Layer-based folders (`Commands/`, `Handlers/`, `Queries/`).
- A handler implementing `ICommandHandler<T>` / `IQueryHandler<T,R>` directly
  instead of deriving from the base class.
- A query with no response type.
- Business logic in an endpoint rather than a handler.
- Any `PackageReference` to MediatR or a comparable mediator library.
- A handler that exists but is not registered in `Add<Module>Module()`.

## Read further

`get_document` on the `4dotnet-csharp-style-guide` MCP server: `adr-0005` (CQRS,
including why we own the base classes) and `adr-0006` (feature slices).
