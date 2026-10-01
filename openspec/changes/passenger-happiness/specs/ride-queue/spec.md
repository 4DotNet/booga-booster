# Spec Delta

## ADDED Requirements

### Requirement: Queue records when each group joined

The queue SHALL record, for every group it accepts, the simulated time at which the group joined, taken from the queue's time source. The join time SHALL be preserved for as long as the group waits and SHALL be the reference from which each member's waiting time is measured.

#### Scenario: Join time is taken from the time source

- **WHEN** a group is enqueued while the queue's time source reads 12:00:00
- **THEN** the group's waiting time read at 12:06:00 is 6 minutes

#### Scenario: Groups keep their own join times

- **WHEN** group A joins at 12:00 and group B joins at 12:04
- **THEN** at 12:10 group A has waited 10 minutes and group B has waited 6 minutes

### Requirement: Queue status reports guest experience

The queue status SHALL report, for every waiting person, their current happiness (eroded by their waiting time), preferred ride intensity and nausea, and SHALL report the average current happiness of all people waiting. When the queue is empty the average happiness SHALL be reported as absent rather than as zero.

#### Scenario: Status includes per-person ratings and the average

- **WHEN** the queue holds two people with current happiness 70 and 80
- **THEN** the status reports each person's ratings and an average happiness of 75

#### Scenario: Empty queue has no average

- **WHEN** the queue holds nobody
- **THEN** the status reports no average happiness

#### Scenario: Status reflects erosion at read time

- **WHEN** the status is read twice, 3 minutes apart, for a group that has waited more than 5 minutes
- **THEN** the second read reports a lower happiness for that group's members and a lower average
