## 1. Shared plumbing (Core)

- [x] 1.1 Verify/implement the DDD domain-model base class in `FourDotnet.BoogaBooster.Core` with lifecycle state (`New`/`Pristine`/`Touched`/`Modified`/`Deleted`) and the `SetX()` change-apply helpers (ADR-0003)
- [x] 1.2 Verify/implement the CQRS base classes in `FourDotnet.BoogaBooster.Core` — `Command`, `Query`, `CommandHandler`, `QueryHandler` — and their DI-based dispatch (ADR-0005; no external mediator)
- [x] 1.3 Add `FourDotnet.BoogaBooster.Core` reference to `FourDotnet.BoogaBooster.Weather`

## 2. Abstractions (Weather.Abstractions)

- [x] 2.1 Add `PrecipitationType` enum (`None`, `Rain`, `Snow`, `Hail`) to `FourDotnet.BoogaBooster.Weather.Abstractions`
- [x] 2.2 Add `WeatherRegime` enum (`Calm`, `Precipitation`, `StrongWind`) and `WeatherConditionDto` (temperature, wind bft, sunshine, precipitation type, active regime) to `.Abstractions`
- [x] 2.3 Define `IWeatherConditionProvider` (returns the current `WeatherConditionDto`) in `.Abstractions`
- [x] 2.4 Confirm `Weather.Abstractions` references only what a public contract needs (no implementation dependencies)

## 3. Domain model (weather-simulation)

- [x] 3.1 Implement validated value objects: `Temperature` (Celsius), `Wind` (Beaufort 0–12), `Sunshine` (bounded intensity), `Precipitation` (type + intensity) — each fully validated on construction (ADR-0003)
- [x] 3.2 Define the moderate default readings and each regime's target readings/step sizes/durations as named constants in one place
- [x] 3.3 Implement the `Weather` aggregate deriving from the Core base class: readings as value objects with public getters/private setters, active regime, and remaining-event duration
- [x] 3.4 Implement intent-revealing `SetX()`/value-object operations that validate before assigning and update lifecycle state (`Modified` vs `Touched`)
- [x] 3.5 Introduce the `IWeatherSampler` randomness seam and a `Random`-backed default implementation (seedable)
- [x] 3.6 Implement `Advance(TimeSpan elapsed, IWeatherSampler sampler)`: decrement the event timer and revert to `Calm` on expiry, compute the regime target, nudge each reading partway toward the target plus a bounded random sample, and apply via the validated operations
- [x] 3.7 Ensure readings never leave valid bounds and that `Calm` drift stays in a bounded band around the defaults (mean-reversion)

## 4. Event behavior (weather-events)

- [x] 4.1 Implement `StartPrecipitation(PrecipitationType type)` on the aggregate — set regime `Precipitation`, chosen type, and ~15 min duration; reject `None`
- [x] 4.2 Implement `StartStrongWind()` on the aggregate — set regime `StrongWind` and ~10 min duration
- [x] 4.3 Verify precipitation targets (cooler temp, higher wind, active precip type) and strong-wind targets (very high wind, lower sunshine, cooler temp) drive the drift correctly
- [x] 4.4 Verify both events auto-expire after their duration and gradually revert to defaults via continued `Advance` ticks

## 5. In-memory state and simulation loop

- [x] 5.1 Implement a thread-safe singleton `IWeatherStore` holding the one `Weather` aggregate, initialized to the moderate defaults, exposing consistent-snapshot reads and guarded mutations
- [x] 5.2 Implement `WeatherSimulationService : BackgroundService` that ticks on a fixed interval via `TimeProvider`, calls `Advance` under the store lock, and writes back
- [x] 5.3 Implement `IWeatherConditionProvider` in the module, reading the current snapshot from `IWeatherStore`

## 6. Integration event publishing (weather-simulation)

- [x] 6.1 Ensure the `integration-messages` change is in place (central `FourDotnet.BoogaBooster.IntegrationMessages` library, publisher, Dapr/RabbitMQ wiring) — prerequisite for real publishing
- [x] 6.2 Add `WeatherUpdateIntegrationEvent` under `Events.Weather` in the IntegrationMessages library, suffixed `IntegrationEvent`, with a `[TopicName(...)]` attribute, carrying temperature, wind, sunshine, precipitation type, and active regime
- [x] 6.3 Introduce an `IWeatherUpdatePublisher` seam in the module (default maps the current snapshot to `WeatherUpdateIntegrationEvent` and publishes via the integration-messages publisher; a no-op implementation lets the module run standalone)
- [x] 6.4 Publish from the simulation loop only when a tick yields a real change (`Modified`, not `Touched`), and from the event command handlers when an event is triggered

## 7. Features (CQRS feature slices)

- [x] 7.1 `Features/GetWeather`: `GetWeatherQuery` + `GetWeatherQueryHandler` returning `WeatherConditionDto` from the store
- [x] 7.2 `Features/StartPrecipitation`: `StartPrecipitationCommand(PrecipitationType)` + handler that mutates the aggregate via `StartPrecipitation` and publishes the update
- [x] 7.3 `Features/StartStrongWind`: `StartStrongWindCommand` + handler that mutates the aggregate via `StartStrongWind` and publishes the update

## 8. Endpoints and module wiring (weather-api)

- [x] 8.1 In the module's `Endpoints` namespace, map a `/weather` `MapGroup`: `GET /weather` → GetWeather, `POST /weather/precipitation` → StartPrecipitation, `POST /weather/strong-wind` → StartStrongWind
- [x] 8.2 Map request DTOs to commands/queries in the endpoints (no business logic in endpoints); return `400` for an unknown precipitation type
- [x] 8.3 Implement `AddWeatherModule()` registering handlers, `IWeatherStore` (singleton), `IWeatherSampler`, `IWeatherUpdatePublisher`, `IWeatherConditionProvider`, and the hosted `WeatherSimulationService`
- [x] 8.4 Implement `MapWeatherEndpoints()` delegating to the endpoint configuration
- [x] 8.5 Wire the module into `FourDotnet.BoogaBooster.Api` with `builder.AddWeatherModule()` and `app.MapWeatherEndpoints()` only (no endpoint logic in the host)

## 9. Tests (xUnit v3, Moq, Bogus; no FluentAssertions)

- [x] 9.1 Add a `FourDotnet.BoogaBooster.Weather.Tests` project (xUnit v3) and add it to `BoogaBooster.slnx`
- [x] 9.2 Value-object tests: valid construction, and rejection of out-of-range values (e.g. wind outside 0–12)
- [x] 9.3 Drift tests: `Calm` readings change only a little per tick and stay in a bounded band around the defaults over many ticks (seeded sampler)
- [x] 9.4 Determinism test: same seed + same `FakeTimeProvider` steps produce the same state sequence
- [x] 9.5 Precipitation tests: effects (cooler temp, higher wind, active type), auto-expiry after ~15 min, and gradual reversion to defaults
- [x] 9.6 Strong-wind tests: effects (very high wind, lower sunshine, cooler temp), auto-expiry after ~10 min, and gradual reversion to defaults
- [x] 9.7 Publishing tests: a changed tick and an event trigger each publish a `WeatherUpdateIntegrationEvent`; a no-op tick publishes nothing (mock `IWeatherUpdatePublisher`)
- [x] 9.8 Store concurrency test: reads during an advance return a consistent snapshot

## 10. Verification

- [x] 10.1 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` pass
- [x] 10.2 Run the backend via the Aspire AppHost and manually exercise `GET /weather`, `POST /weather/precipitation`, and `POST /weather/strong-wind`
- [x] 10.3 Confirm `WeatherUpdateIntegrationEvent` messages are published on weather changes (observe via the RabbitMQ management plugin / a test subscriber)
