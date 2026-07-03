## ADDED Requirements

### Requirement: Moderate default weather at startup

The weather service SHALL initialize a single in-memory world weather at moderate defaults: a temperature of approximately 20 °C, pleasant (not too warm) sunshine, light wind of approximately 2 Beaufort, and no precipitation. The active regime SHALL start as `Calm`.

#### Scenario: Fresh start reports the defaults

- **WHEN** the weather service starts and has not yet ticked
- **THEN** the current conditions report a temperature near 20 °C, light wind near 2 bft, pleasant sunshine, and no precipitation
- **AND** the active regime is `Calm`

### Requirement: Valid weather readings enforced by the domain model

The `Weather` domain model SHALL keep its readings within valid, plausible bounds at all times, using validated value objects (temperature, wind in Beaufort 0–12, sunshine intensity, precipitation type and intensity). It SHALL be impossible to construct or advance the model into an invalid reading.

#### Scenario: Wind stays within the Beaufort scale

- **WHEN** the simulation advances under any regime
- **THEN** the wind reading is always between 0 and 12 Beaufort inclusive

#### Scenario: Invalid value object construction is rejected

- **WHEN** a weather value object is constructed with an out-of-range value (e.g. negative wind or a sunshine intensity outside its bounds)
- **THEN** construction fails with a domain validation error and no invalid reading is produced

### Requirement: Autonomous bounded random drift around the defaults

While in the `Calm` regime the weather service SHALL, on each simulation tick, change its readings by a small random amount while remaining close to the defaults (a bounded, mean-reverting random walk). Readings SHALL NOT wander arbitrarily far from the defaults over time.

#### Scenario: Readings change a little each tick

- **WHEN** the simulation ticks repeatedly in the `Calm` regime
- **THEN** each reading changes by only a small amount per tick rather than jumping

#### Scenario: Readings stay near the defaults over many ticks

- **WHEN** the simulation runs for many ticks in the `Calm` regime with no user events
- **THEN** temperature, wind, and sunshine remain within a bounded band around their defaults and trend back toward them

### Requirement: Mean-reversion toward the active regime target

Each simulation tick SHALL nudge every reading a fraction of the way from its current value toward the target value of the active regime, plus a small bounded random sample. When the regime is `Calm` the target is the defaults; the reversion SHALL be gradual so readings ease toward the target rather than snapping to it.

#### Scenario: A reading eases toward its target

- **WHEN** a reading is far from the active regime's target and the simulation ticks
- **THEN** the reading moves partway toward the target on that tick, not all the way

### Requirement: Deterministic, injectable simulation clock and randomness

The simulation SHALL advance on a periodic tick driven by an injected `TimeProvider`, and its randomness SHALL be supplied through an injectable sampler seam. Given the same seed and the same time progression, the sequence of weather states SHALL be reproducible.

#### Scenario: Reproducible evolution under a fixed seed

- **WHEN** the simulation is advanced with a fake `TimeProvider` and a seeded sampler
- **THEN** repeating the run with the same seed and the same time steps produces the same sequence of weather states

#### Scenario: Ticks are driven by the injected time provider

- **WHEN** the injected `TimeProvider` advances by one tick interval
- **THEN** the simulation performs exactly one advance step

### Requirement: Single thread-safe in-memory world state

The weather service SHALL hold exactly one world weather instance in memory behind a thread-safe store shared by the simulation loop, the read query, and the event commands. Concurrent reads SHALL NOT observe a partially advanced weather state, and there SHALL be no persistent data store.

#### Scenario: Reads never see a half-updated state

- **WHEN** a read occurs while the simulation loop is advancing the weather
- **THEN** the read returns a consistent snapshot, either fully before or fully after the advance, never a partially updated one

#### Scenario: State does not survive a restart

- **WHEN** the weather service restarts
- **THEN** the weather re-initializes to the moderate defaults with no retained history

### Requirement: Publish a weather update event on every changed update

On every weather update that actually changes conditions — each simulation tick whose advance produces a real change, and the moment a user event is triggered — the weather service SHALL publish a `WeatherUpdateIntegrationEvent` carrying the new conditions, so other services are notified of the change. The event SHALL be defined in the central integration-messages library (`Events.Weather` namespace, `IntegrationEvent` suffix, annotated with its `[TopicName]`) and published through the shared integration-messages publisher. A tick that produces no change SHALL NOT publish an event.

#### Scenario: A changed tick publishes an update

- **WHEN** a simulation tick advances the weather to different conditions
- **THEN** a `WeatherUpdateIntegrationEvent` is published carrying the new temperature, wind, sunshine, precipitation, active regime, and nice-weather indicator

#### Scenario: Triggering an event publishes an update

- **WHEN** a user triggers a precipitation or strong-wind event
- **THEN** a `WeatherUpdateIntegrationEvent` reflecting the changed conditions is published

#### Scenario: An unchanged tick publishes nothing

- **WHEN** a simulation tick produces no actual change to the conditions
- **THEN** no `WeatherUpdateIntegrationEvent` is published for that tick

### Requirement: Weather updates carry a nice-weather indicator

Every weather update (the snapshot served to consumers and the published `WeatherUpdateIntegrationEvent`) SHALL include a "nice weather" indicator: a value in the range [0, 1] where 1 means pleasant, moderate weather and 0 means bad weather such as a severe storm or heavy precipitation.

#### Scenario: Moderate weather scores as nice

- **WHEN** the weather is at its moderate defaults with no precipitation and light wind
- **THEN** the nice-weather indicator is 1

#### Scenario: A storm or heavy precipitation scores as not nice

- **WHEN** the weather is a severe storm (very strong wind) or has heavy precipitation
- **THEN** the nice-weather indicator is 0 (or near 0)
