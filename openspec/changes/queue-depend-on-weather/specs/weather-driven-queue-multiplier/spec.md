## ADDED Requirements

### Requirement: Queue subscribes to the weather-update integration event

The Queue module SHALL subscribe to the `WeatherUpdateIntegrationEvent` by mapping a minimal-API endpoint annotated with Dapr's `WithTopic("weather-updated")`, matching the `[TopicName]` declared on the event. The subscription SHALL be mapped as part of the module's `MapQueueEndpoints` and SHALL NOT be declared through static Dapr subscription YAML.

#### Scenario: Subscriber endpoint receives a weather update

- **WHEN** a `WeatherUpdateIntegrationEvent` is published to the `weather-updated` topic
- **THEN** the Queue module's subscriber endpoint is invoked with the strongly-typed event
- **AND** it responds successfully so the message is acknowledged

#### Scenario: Topic name matches the event contract

- **WHEN** the subscriber's `WithTopic(...)` value is compared to the event's `[TopicName]` attribute
- **THEN** both are `weather-updated` so published messages are delivered to the endpoint

### Requirement: Latest nice-weather indicator is held in shared state

The Queue module SHALL maintain a thread-safe, in-memory, singleton weather-influence state that stores the most recently received `NiceWeather` indicator. Each received `WeatherUpdateIntegrationEvent` SHALL overwrite the stored value with the event's `NiceWeather`. The stored value SHALL be clamped to the range `[0, 1]`.

#### Scenario: Newest event wins

- **WHEN** two `WeatherUpdateIntegrationEvent` messages arrive in order with different `NiceWeather` values
- **THEN** the weather-influence state reflects the value from the later message

#### Scenario: Out-of-range indicator is clamped

- **WHEN** an event carries a `NiceWeather` value outside `[0, 1]`
- **THEN** the stored value is clamped into `[0, 1]`

#### Scenario: State is read consistently while updated

- **WHEN** the background filler reads the weather-influence state while updates arrive concurrently
- **THEN** the read returns a valid indicator in `[0, 1]` without throwing

### Requirement: Neutral default before any weather is received

Until the first `WeatherUpdateIntegrationEvent` is received, the weather-influence state SHALL report a configurable neutral default multiplier so the filler produces a sensible arrival rate at startup. The default SHALL be configurable and default to a value that neither suppresses nor inflates arrivals.

#### Scenario: Filler uses the neutral default at startup

- **WHEN** a fill cycle runs before any weather-update event has been received
- **THEN** the arrival count is scaled by the configured neutral default rather than being zeroed or left unscaled by an undefined value

### Requirement: Nice-weather indicator multiplies the arrival count

Each fill cycle SHALL scale its planned arrival count by a multiplier derived from the current `NiceWeather` indicator before enqueuing guests. Higher `NiceWeather` SHALL yield more arrivals and lower `NiceWeather` SHALL yield fewer, monotonically. The mapping from indicator to multiplier SHALL be configurable (including how sharply low values suppress arrivals and an optional ceiling for very nice weather), while the base arrival bounds and fill interval remain governed by the existing queue options.

#### Scenario: Nice weather yields more arrivals than bad weather

- **WHEN** one fill cycle runs with a high `NiceWeather` indicator and another identical cycle runs with a low `NiceWeather` indicator
- **THEN** the cycle under the higher indicator enqueues more guests than the cycle under the lower indicator

#### Scenario: Worst weather stops arrivals

- **WHEN** a fill cycle runs while `NiceWeather` is at or near `0`
- **THEN** the scaled arrival count is zero and no guests are enqueued that cycle

#### Scenario: Arrivals resume as weather improves

- **WHEN** the `NiceWeather` indicator rises from near `0` toward `1` across successive cycles
- **THEN** the number of guests enqueued per cycle increases accordingly

#### Scenario: Weather is re-evaluated each cycle

- **WHEN** the weather-influence state changes between two fill cycles
- **THEN** the later cycle's arrival count reflects the newer indicator

### Requirement: Multiplier preserves existing filling invariants

Applying the weather multiplier SHALL NOT change the queue's group-composition and capacity rules. Scaled arrivals SHALL still be partitioned into varied group sizes within the configured bounds, and the fill SHALL still stop at a ride's maximum queue length. The scaled arrival count SHALL never be negative.

#### Scenario: Scaled arrivals still form valid groups

- **WHEN** a fill cycle produces a positive scaled arrival count
- **THEN** the arrivals are split into groups whose sizes stay within the configured group-size bounds and sum to the scaled count

#### Scenario: Capacity still bounds a scaled fill

- **WHEN** a scaled fill cycle would exceed a ride's maximum queue length
- **THEN** enqueuing stops at capacity and no further guests are added that cycle
