## ADDED Requirements

### Requirement: Background service continuously fills ride queues

A background service SHALL run for the lifetime of the host and periodically add newly arrived guests to each ride's queue. The interval between fill cycles SHALL be driven by an injectable clock/timer so the behavior is deterministic and testable.

#### Scenario: Guests are added over successive cycles

- **WHEN** the background service runs for several fill cycles against a ride queue
- **THEN** the ride queue length increases across cycles as new arrivals are enqueued

#### Scenario: Service starts and stops with the host

- **WHEN** the host starts and later shuts down
- **THEN** the fill service begins filling on start and stops filling on shutdown without throwing

### Requirement: Default arrival rate when weather is not yet integrated

Until the Weather module is integrated, the fill service SHALL use a configurable fixed arrival rate — by default between 4 and 8 people per fill cycle (one cycle per minute) — partitioned into a variety of group sizes. The bounds and interval SHALL be configurable.

#### Scenario: Default rate enqueues 4 to 8 people per cycle

- **WHEN** a fill cycle runs with the default configuration and the queue is below capacity
- **THEN** between 4 and 8 people are enqueued that cycle, split across one or more groups whose sizes vary

### Requirement: Arrival rate is modulated by weather

The number of guests arriving per fill cycle SHALL scale with the current weather condition obtained from the Weather module. Good weather SHALL yield a higher arrival rate and bad weather SHALL yield a lower arrival rate, down to few or no arrivals in the worst conditions.

#### Scenario: Good weather yields more arrivals

- **WHEN** a fill cycle runs while the weather reports a good condition
- **THEN** the number of guests enqueued that cycle is greater than the number enqueued for the same ride under a bad condition

#### Scenario: Bad weather sharply reduces arrivals

- **WHEN** a fill cycle runs while the weather reports a bad condition
- **THEN** few or no guests are enqueued that cycle

#### Scenario: Weather is re-evaluated each cycle

- **WHEN** the weather condition changes between two fill cycles
- **THEN** the arrival rate of the later cycle reflects the newer condition

### Requirement: Arrivals include individuals and groups

Each fill cycle SHALL be able to enqueue both lone guests and multi-guest groups, and every enqueued group SHALL preserve its group relationship in the queue.

#### Scenario: A cycle enqueues a mix of individuals and groups

- **WHEN** a fill cycle produces both a lone arrival and a group arrival
- **THEN** the lone guest is enqueued as a group of one and the group is enqueued as a contiguous group with a shared group identifier

### Requirement: Filling respects a maximum queue length

The fill service SHALL NOT grow a ride's queue beyond a configured maximum length. When a ride's queue is at capacity, that ride SHALL receive no further arrivals until space is freed by boarding.

#### Scenario: No arrivals added at capacity

- **WHEN** a fill cycle runs against a ride whose queue is already at its maximum length
- **THEN** no guests are enqueued for that ride and its queue length is unchanged
