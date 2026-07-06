## ADDED Requirements

### Requirement: Waiting groups evacuate the queue in unsafe weather

When the Queue module receives a weather update whose `NiceWeather` indicator is below the configured closure threshold, it SHALL evacuate the ride's waiting line — every group currently queued for that ride SHALL be removed so no group waits for a ride that has closed for the weather. The closure threshold SHALL be configurable via `QueueModuleOptions` and default to the same value as the ride's weather closure threshold (`0.25`).

#### Scenario: Unsafe weather drains the line

- **WHEN** a ride has waiting groups and a weather update arrives with `NiceWeather` below the closure threshold
- **THEN** all of that ride's waiting groups are removed and the ride's queue is empty

#### Scenario: Safe weather leaves the line intact

- **WHEN** a ride has waiting groups and a weather update arrives with `NiceWeather` at or above the closure threshold
- **THEN** the waiting groups remain in the queue

### Requirement: Evacuation composes with arrival suppression

Evacuating the line SHALL work alongside the existing weather-driven arrival scaling: in unsafe weather the line both drains and takes on no new arrivals, so the queue stays empty until the weather recovers, at which point arrivals resume under the existing multiplier rules.

#### Scenario: No arrivals refill an evacuated line

- **WHEN** the line has been evacuated and the weather is still unsafe
- **THEN** subsequent fill cycles enqueue no groups and the line stays empty

#### Scenario: Arrivals resume after the weather recovers

- **WHEN** the weather rises back above the closure threshold after an evacuation
- **THEN** later fill cycles enqueue groups again according to the current nice-weather multiplier

### Requirement: The ride queue supports evacuation

The `RideQueue` domain SHALL provide an operation that removes all waiting groups at once (evacuation), leaving a valid, empty queue. Evacuating an already-empty queue SHALL be a no-op.

#### Scenario: Evacuate clears every group

- **WHEN** a `RideQueue` holding several groups is evacuated
- **THEN** it holds no groups afterward and remains a valid queue

#### Scenario: Evacuating an empty queue is harmless

- **WHEN** an empty `RideQueue` is evacuated
- **THEN** it remains empty and no error occurs
