## ADDED Requirements

### Requirement: Read the current weather

The weather module SHALL expose a minimal-API endpoint that returns the current weather conditions — temperature, wind (in Beaufort), sunshine, precipitation type, the active regime, and the nice-weather indicator — as a DTO. The read SHALL be implemented as a `GetWeather` query feature dispatched to its handler.

#### Scenario: Get current weather

- **WHEN** a client sends `GET /weather`
- **THEN** the response is `200 OK` with a body describing the current temperature, wind, sunshine, precipitation type, active regime, and nice-weather indicator

### Requirement: Trigger precipitation over HTTP

The weather module SHALL expose a minimal-API endpoint that lets a client trigger a precipitation event, specifying the type (rain, snow, or hail). The endpoint SHALL map the request DTO to a `StartPrecipitation` command dispatched to its handler.

#### Scenario: Trigger rain

- **WHEN** a client sends `POST /weather/precipitation` with type rain
- **THEN** the response indicates success and a precipitation event of type rain becomes active

#### Scenario: Reject an unknown precipitation type

- **WHEN** a client sends `POST /weather/precipitation` with an unsupported type
- **THEN** the response is a `400 Bad Request` and no event is started

### Requirement: Trigger strong wind over HTTP

The weather module SHALL expose a minimal-API endpoint that lets a client trigger a strong-wind event. The endpoint SHALL map the request to a `StartStrongWind` command dispatched to its handler.

#### Scenario: Trigger strong wind

- **WHEN** a client sends `POST /weather/strong-wind`
- **THEN** the response indicates success and a strong-wind event becomes active

### Requirement: Module owns its registration and endpoint mapping

The weather module SHALL expose exactly two extension methods — one on the host builder that registers all of the module's handlers, the store, the sampler, the hosted simulation service, and the weather-condition provider, and one on the web application that maps all of the module's endpoints under a shared route group. The API host SHALL contain no weather endpoint mappings.

#### Scenario: Host composes the module with two calls

- **WHEN** the API host wires up the weather module
- **THEN** it calls `AddWeatherModule()` on the builder and `MapWeatherEndpoints()` on the app, and contains no weather endpoint logic itself

### Requirement: Weather conditions exposed to other modules

The `Weather.Abstractions` project SHALL provide an `IWeatherConditionProvider` contract that returns the current weather conditions, so other modules can read the weather without referencing the weather module project. The provider SHALL reflect the same current state served by the read endpoint.

#### Scenario: Another module reads the weather via the provider

- **WHEN** a consuming module resolves `IWeatherConditionProvider` and requests the current conditions
- **THEN** it receives the current weather conditions consistent with what `GET /weather` would return
- **AND** it does not reference the weather module project, only `Weather.Abstractions`
