## ADDED Requirements

### Requirement: Weather graded into a ride-safety level

The ride SHALL assess the current weather into exactly one of three safety levels — **Clear**, **Caution**, or **Unsafe** — derived from the Weather module's `NiceWeather` indicator. The band thresholds SHALL be configurable, defaulting to: `Unsafe` when `NiceWeather` is below `0.25`, `Caution` when `NiceWeather` is at least `0.25` but below `0.5`, and `Clear` when `NiceWeather` is at least `0.5`. The assessment SHALL read the conditions through the `IWeatherConditionProvider` contract from `Weather.Abstractions` and SHALL be recomputed on every simulation tick before the ride advances.

#### Scenario: Pleasant weather is Clear

- **WHEN** the current `NiceWeather` indicator is at or above the Clear threshold
- **THEN** the ride's weather-safety level is `Clear`

#### Scenario: Deteriorating weather is Caution

- **WHEN** the current `NiceWeather` indicator is at or above the Caution threshold but below the Clear threshold
- **THEN** the ride's weather-safety level is `Caution`

#### Scenario: Bad weather is Unsafe

- **WHEN** the current `NiceWeather` indicator is below the closure threshold
- **THEN** the ride's weather-safety level is `Unsafe`

#### Scenario: Thresholds are configurable

- **WHEN** the configured Clear and closure thresholds are changed and the same `NiceWeather` value is assessed
- **THEN** the resulting level reflects the configured thresholds, not the defaults

### Requirement: Unsafe weather is a blocking safety interlock

While the weather-safety level is `Unsafe`, the ride SHALL report the safety reason `UnsafeWeather` and SHALL NOT be safe to start. This interlock SHALL gate the guarded lifecycle transitions exactly as the other safety interlocks do: `Loading → Safe` and `Safe → Started` SHALL be rejected while the weather is `Unsafe`. `Caution` and `Clear` levels SHALL NOT raise this interlock.

#### Scenario: Unsafe weather blocks becoming safe

- **WHEN** the ride is `Loading` with every restraint secured and load balanced, but the weather is `Unsafe`
- **AND** a transition to `Safe` is requested
- **THEN** the request is rejected with a domain validation error citing the weather
- **AND** the ride's state remains `Loading`

#### Scenario: Unsafe weather blocks starting

- **WHEN** the ride is `Safe` and the weather becomes `Unsafe`
- **AND** a transition to `Started` is requested
- **THEN** the request is rejected and the ride does not start

#### Scenario: Caution weather does not block operation

- **WHEN** the ride is `Safe` and the weather is `Caution` with all other interlocks satisfied
- **AND** a transition to `Started` is requested
- **THEN** the request is accepted and the ride starts

### Requirement: The ride cannot be loaded in unsafe weather

While the weather-safety level is `Unsafe`, the ride SHALL NOT begin loading: the `Idle → Loading` transition SHALL be rejected. When the weather returns to `Caution` or `Clear`, loading SHALL again be permitted from `Idle`.

#### Scenario: Loading is refused in unsafe weather

- **WHEN** the ride is `Idle` and the weather is `Unsafe`
- **AND** a transition to `Loading` is requested
- **THEN** the request is rejected and the ride remains `Idle`

#### Scenario: Loading resumes when weather clears

- **WHEN** the ride is `Idle`, the weather returns to `Clear`, and a transition to `Loading` is requested
- **THEN** the request is accepted and the ride becomes `Loading`

### Requirement: Unsafe weather automatically closes a running ride with a controlled stop

When the weather becomes `Unsafe` while the ride is `Started`, the ride SHALL automatically begin a **controlled** stop by transitioning `Started → Stopping` (cutting engine power and applying the brakes) without an operator command. This SHALL NOT be an emergency stop. Once the ride reaches a complete rest it SHALL, through the existing automatic transitions, move `Stopping → Offloading`, release the safety constraints, unload the passengers, and return to `Idle` when empty.

#### Scenario: A running ride stops when weather turns unsafe

- **WHEN** the ride is `Started` and the weather becomes `Unsafe`
- **THEN** on the next advance the ride's state becomes `Stopping`
- **AND** engine power is cut and the brakes are applied

#### Scenario: The stopped ride unloads and returns to idle

- **WHEN** a weather-closed ride in `Stopping` reaches a complete rest
- **THEN** the ride moves to `Offloading`, its safety constraints are released, and once the last rider has left it returns to `Idle`

#### Scenario: Closure is not an emergency stop

- **WHEN** the weather closes a `Started` ride
- **THEN** the ride enters `Stopping`, not `EmergencyStop`

### Requirement: Unsafe weather automatically unloads a loaded, at-rest ride

When the weather becomes `Unsafe` while the ride is `Loading` or `Safe` (at rest, with passengers possibly aboard), the ride SHALL automatically transition to `Offloading` so passengers disembark, then return to `Idle` when empty. An empty `Idle` ride SHALL simply remain `Idle`.

#### Scenario: A loading ride offloads its passengers

- **WHEN** the ride is `Loading` with passengers aboard and the weather becomes `Unsafe`
- **THEN** on the next advance the ride's state becomes `Offloading`
- **AND** once every seat is empty the ride returns to `Idle`

#### Scenario: A safe ride offloads its passengers

- **WHEN** the ride is `Safe` and the weather becomes `Unsafe`
- **THEN** the ride transitions to `Offloading` and unloads, returning to `Idle` when empty

#### Scenario: An idle empty ride stays idle

- **WHEN** the ride is `Idle` and empty and the weather becomes `Unsafe`
- **THEN** the ride remains `Idle`

### Requirement: Weather-driven closure uses automatic transitions only

The weather-driven closure and loading-block behaviours SHALL be automatic (condition-driven) and SHALL NOT appear in the ride's set of operator-triggerable available transitions.

#### Scenario: Weather closure is never offered as a button

- **WHEN** telemetry is read while the weather is `Unsafe` and closing a running ride
- **THEN** the available operator transitions do not include any weather-forced transition

### Requirement: Telemetry carries the weather-safety level

Ride telemetry SHALL include the current weather-safety level (`Clear`, `Caution`, or `Unsafe`) so consumers can display how the weather affects the ride's safety. When the interlock is active the telemetry's safety reason SHALL be `UnsafeWeather`.

#### Scenario: Telemetry reports the current level

- **WHEN** telemetry is read while the weather is `Caution`
- **THEN** the telemetry's weather-safety level is `Caution`

#### Scenario: Telemetry reports the unsafe-weather reason

- **WHEN** telemetry is read while the weather is `Unsafe`
- **THEN** the telemetry's weather-safety level is `Unsafe`
- **AND** the safety reason is `UnsafeWeather`
