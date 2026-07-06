## ADDED Requirements

### Requirement: Security panel shows a weather-safety indicator

The security panel SHALL display a weather-safety indicator that reflects how the current weather affects the ride's safety, in one of three states — **Clear**, **Caution**, or **Unsafe** — driven by the weather-safety level carried on ride telemetry. The indicator SHALL be a distinct row alongside the existing load and security rows.

#### Scenario: Indicator shows the clear state

- **WHEN** the ride telemetry reports a weather-safety level of `Clear`
- **THEN** the security panel's weather row shows the clear state with wording indicating the weather is safe for the ride

#### Scenario: Indicator shows the caution state

- **WHEN** the ride telemetry reports a weather-safety level of `Caution`
- **THEN** the weather row shows the caution state with wording indicating the weather is deteriorating

#### Scenario: Indicator shows the unsafe state

- **WHEN** the ride telemetry reports a weather-safety level of `Unsafe`
- **THEN** the weather row shows the unsafe state with wording indicating the ride is closed for the weather

#### Scenario: Indicator tracks telemetry changes

- **WHEN** the telemetry's weather-safety level changes between updates
- **THEN** the weather row updates to the new state on the next telemetry update

### Requirement: Weather indicator meets accessibility standards

Each weather-safety state SHALL be conveyed by an icon **and** text, never by colour alone, and the security panel SHALL continue to pass AXE / WCAG AA checks with the added row.

#### Scenario: State is conveyed beyond colour

- **WHEN** any weather-safety state is shown
- **THEN** the state is distinguishable by its icon and worded text independent of colour

#### Scenario: Panel passes accessibility checks

- **WHEN** the security panel is rendered in any weather-safety state
- **THEN** it passes AXE / WCAG AA checks

### Requirement: Telemetry model carries the weather-safety level

The frontend ride-telemetry model and its telemetry source SHALL include the weather-safety level so the security panel can bind to it. A missing or unrecognized value SHALL default to `Clear`.

#### Scenario: Model exposes the level from the server

- **WHEN** a telemetry payload includes a weather-safety level
- **THEN** the normalized telemetry model exposes that level to the panel

#### Scenario: Missing level defaults to clear

- **WHEN** a telemetry payload omits or carries an unrecognized weather-safety level
- **THEN** the normalized model reports `Clear`
