## Why

The `Weather` bounded context is currently an empty scaffold (`Class1.cs`), yet other modules already depend on it: the ride-queue module consumes an `IWeatherConditionProvider`, and the integration-messages work expects a `WeatherChangedIntegrationEvent` to originate here. We need a self-driving weather simulation that produces a believable, continuously changing climate and lets users perturb it, so the rest of the park has real conditions to react to.

## What Changes

- Implement the `Weather` module as an **autonomous weather simulation**: it starts at moderate defaults (≈20 °C, pleasant sunshine, light wind ≈2 bft, no precipitation) and, on a periodic tick, drifts randomly while staying close to those defaults (bounded, mean-reverting random walk).
- Add a **user-triggered precipitation event** (rain, snow, or hail). While active: temperature drops and wind picks up a bit. It runs for ≈15 minutes, then stops and the weather gradually reverts to defaults.
- Add a **user-triggered strong-wind event**: sunshine fades, temperature drops, and wind climbs to very strong. It runs for up to ≈10 minutes, then wind eases and sunshine/temperature gradually return to defaults.
- Expose **minimal-API endpoints** to read the current weather and to trigger the two events, mapped from the module per ADR-0007.
- Publish the current conditions to the rest of the system through the `IWeatherConditionProvider` abstraction in `Weather.Abstractions` (the contract the ride-queue module already consumes).
- On **every weather update** (each simulation tick that changes conditions, plus the moment an event is triggered), publish a `WeatherUpdateIntegrationEvent` so other services are notified of weather changes, using the central integration-messages publisher.
- Keep the single "world" weather state **in memory** (no data store) behind a thread-safe store shared by the simulation loop, the query, and the event commands.

## Capabilities

### New Capabilities

- `weather-simulation`: The autonomous baseline — the rich `Weather` domain model and its invariants, the moderate defaults, the bounded mean-reverting random drift, the in-memory single-world state, the periodic hosted tick loop that advances the simulation deterministically, and the publication of a `WeatherUpdateIntegrationEvent` on every weather update.
- `weather-events`: The two user-initiated perturbations — precipitation (rain/snow/hail) and strong wind — including their immediate and ongoing effects, their durations (~15 min / up to ~10 min), and the gradual reversion to defaults after they end.
- `weather-api`: The module's HTTP surface (read current weather, trigger precipitation, trigger strong wind) as minimal-API feature slices, plus the `IWeatherConditionProvider` contract exposed to other modules from `Weather.Abstractions`.

### Modified Capabilities

<!-- None — no existing specs change their requirements. -->

## Impact

- **Modules**: fills in `src/Weather/FourDotnet.BoogaBooster.Weather` (domain model, features, endpoints, hosted simulation, in-memory store) and `src/Weather/FourDotnet.BoogaBooster.Weather.Abstractions` (`IWeatherConditionProvider`, weather + precipitation DTOs/enums).
- **Shared/Core**: consumes the DDD base classes (ADR-0003) and the CQRS command/query/handler base classes (ADR-0005) from `FourDotnet.BoogaBooster.Core`; if those are not yet present they are added there as shared plumbing.
- **API host**: `FourDotnet.BoogaBooster.Api` composes the module via `builder.AddWeatherModule()` and `app.MapWeatherEndpoints()` only (no endpoint logic in the host, per ADR-0007).
- **Downstream consumers**: satisfies the `IWeatherConditionProvider` dependency the `maintaining-the-people-queue` change relies on.
- **Cross-change integration**: the simulation publishes a `WeatherUpdateIntegrationEvent` on every weather update via the central `FourDotnet.BoogaBooster.IntegrationMessages` publisher (`AddBoogaBoosterIntegrationMessages()`), following that library's conventions (`Events.Weather` namespace, `IntegrationEvent` suffix, `[TopicName]` attribute). This makes the `integration-messages` change a prerequisite for the publishing behavior; the rest of the module functions without it.
- **Testing**: adds a Weather test project (xUnit v3, Moq, Bogus; no FluentAssertions) exercising drift bounds, mean-reversion, event effects/durations, and reversion using seeded randomness and a fake `TimeProvider`.
- **Dependencies**: no new NuGet packages beyond the framework, `TimeProvider`, and the existing Aspire/service-defaults stack.
