## ADDED Requirements

### Requirement: Queue status exposes each guest's mood

The queue status SHALL list, for each queued guest, their guest number, current happiness (with queue-wait decay applied at the time of the read), preferred G and nausea, in queue order. Each queued group SHALL record the time it was enqueued, taken from the injected `TimeProvider`.

#### Scenario: Status includes current happiness
- **WHEN** the queue status is read for a guest who has waited 10 minutes with starting happiness 80
- **THEN** that guest is listed with happiness of approximately 71.4

#### Scenario: Enqueue time is recorded
- **WHEN** a group is enqueued
- **THEN** the group records the current `TimeProvider` time as its enqueue time
