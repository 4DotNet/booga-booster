## ADDED Requirements

### Requirement: Fetch the current weather from the server

The app SHALL fetch the current weather conditions from the backend via `GET /weather` and expose them to the UI as a signal, behind a `WeatherSource` seam registered with an injection token (mirroring the existing telemetry-source pattern). Components SHALL NOT call `HttpClient` directly.

#### Scenario: Current conditions are loaded

- **WHEN** the weather feature initializes
- **THEN** it requests `GET /weather`
- **AND** on success exposes the returned conditions (temperature, wind, sunshine, precipitation, regime, nice-weather indicator) as the current conditions signal

#### Scenario: Conditions are consumed through the source seam

- **WHEN** a component needs the weather
- **THEN** it reads the conditions from the injected `WeatherSource`/state service
- **AND** does not depend on `HttpClient` itself

### Requirement: Poll so the panel reflects the weather's lifecycle

Because the server weather drifts on its own, the client SHALL poll `GET /weather` on a recurring interval and update the exposed conditions each time, so the panel stays current without user action. Polling SHALL be cleaned up when the feature is destroyed.

#### Scenario: Conditions refresh over time

- **WHEN** the weather has been displayed for one polling interval
- **THEN** the client re-fetches `GET /weather` and the exposed conditions update to the latest values

#### Scenario: Polling stops on teardown

- **WHEN** the weather feature is destroyed
- **THEN** no further polling requests are made

### Requirement: Post weather disturbances to the server

The client SHALL expose operations that start precipitation (`POST /weather/precipitation` with a body of `{ type }` where type is `Rain`, `Snow`, or `Hail`) and start strong wind (`POST /weather/strong-wind`). After a disturbance succeeds, the client SHALL refresh the current conditions immediately rather than waiting for the next poll.

#### Scenario: Start precipitation

- **WHEN** the start-precipitation operation is invoked with a type
- **THEN** the client sends `POST /weather/precipitation` with that type
- **AND** on success re-fetches the current conditions

#### Scenario: Start strong wind

- **WHEN** the start-strong-wind operation is invoked
- **THEN** the client sends `POST /weather/strong-wind`
- **AND** on success re-fetches the current conditions

### Requirement: Normalize the server weather contract

The client SHALL map the server DTO (camelCase fields) into typed models, accepting the `precipitation` and `regime` enums whether the server emits them as numeric values or strings, so the UI always works with stable string labels.

#### Scenario: Numeric enums are mapped to labels

- **WHEN** the server returns `precipitation` and `regime` as numbers
- **THEN** the client maps them to their corresponding string labels (e.g. precipitation `1` → `Rain`, regime `2` → `StrongWind`)

#### Scenario: String enums pass through

- **WHEN** the server returns `precipitation` and `regime` as strings
- **THEN** the client uses those string values directly

### Requirement: Reach the API through a dev proxy

In development the app SHALL call the weather API using relative `/api` paths that a dev-server proxy forwards to the Aspire-managed API. The proxy target SHALL be taken from the Aspire service-discovery environment variable, with a localhost fallback, and SHALL accept the development certificate.

#### Scenario: Relative calls are proxied to the API

- **WHEN** the app calls `/api/weather` (or the disturbance paths) from the dev server
- **THEN** the request is forwarded to the API resolved from the Aspire service-discovery environment variable
- **AND** the `/api` prefix is stripped so it maps to the API's `/weather` routes

#### Scenario: Runs under Aspire and standalone

- **WHEN** the dev server starts under the Aspire AppHost
- **THEN** the proxy targets the injected API endpoint
- **AND** when started standalone it falls back to a local default target
