# Spec Delta

## Purpose

Gives every guest a mood (happiness, preferred ride intensity and nausea) from the moment they join the queue. Defines how waiting erodes that mood and how the mood leaves the queue with the guest when their group boards.

## ADDED Requirements

### Requirement: Guests carry three mood scores

Every guest SHALL carry a happiness score, a preferred ride intensity score and a nausea score. Each score SHALL lie within 0 to 100 inclusive, where 0 is the lowest (very sad, gentlest preference, no nausea) and 100 is the highest (extremely happy, most intense preference, extremely nauseous). A score outside that range SHALL be rejected as a validation error.

#### Scenario: Scores within range are accepted

- **WHEN** a guest is created with happiness 0, preferred intensity 100 and nausea 50
- **THEN** the guest is created with exactly those scores

#### Scenario: Out-of-range score is rejected

- **WHEN** a guest is created with happiness 101 or preferred intensity −1
- **THEN** creation fails with a validation error and no guest is created

### Requirement: Arriving guests start fairly to quite happy

When a guest is generated for the queue, their initial happiness SHALL be drawn uniformly from the whole numbers 65 to 85 inclusive. Their preferred ride intensity SHALL be drawn uniformly from the whole numbers 50 to 100 inclusive. Their nausea SHALL be 0. The draws SHALL come from the queue's seeded random source, so a fixed seed reproduces the same guests.

#### Scenario: Initial scores fall in their ranges

- **WHEN** 1,000 guests are generated
- **THEN** every guest's happiness is between 65 and 85 inclusive
- **AND** every guest's preferred intensity is between 50 and 100 inclusive
- **AND** every guest's nausea is 0

#### Scenario: Same seed, same guests

- **WHEN** two generators with the same seed each generate 20 guests
- **THEN** both sequences have identical happiness and preferred intensity scores

### Requirement: Long waits lower happiness exponentially

A waiting guest's happiness SHALL stay at its arrival value for the first 5 minutes in the queue. After that it SHALL decline by `5 · (2^((w − 5 min) / 5 min) − 1)` points, where `w` is the time the guest has waited, so each further 5 minutes costs more than the 5 minutes before. Happiness SHALL never drop below 0. Wait time SHALL be measured from when the guest's group joined the queue.

#### Scenario: No loss within the first five minutes

- **WHEN** a guest who arrived with happiness 80 has waited 4 minutes 59 seconds
- **THEN** their happiness is 80

#### Scenario: Loss accelerates with wait time

- **WHEN** a guest who arrived with happiness 80 has waited 10, 15 and then 20 minutes
- **THEN** their happiness is 75, 65 and 45 respectively

#### Scenario: Happiness bottoms out at zero

- **WHEN** a guest who arrived with happiness 65 has waited 30 minutes
- **THEN** their happiness is 0

### Requirement: Queue status reports guest mood

The queue status SHALL include, for every waiting guest, their current happiness (after any wait-time decline), their preferred ride intensity and their nausea. It SHALL also report the average current happiness of everyone waiting. When nobody is waiting, the average SHALL be reported as absent, not as 0.

#### Scenario: Average happiness of the waiting line

- **WHEN** three guests with current happiness 60, 70 and 80 are waiting
- **THEN** the queue status reports an average happiness of 70

#### Scenario: Status reflects the wait-time decline

- **WHEN** a guest who arrived with happiness 80 has waited 15 minutes
- **THEN** the queue status reports that guest's happiness as 65

#### Scenario: Empty queue has no average

- **WHEN** nobody is waiting in the queue
- **THEN** the queue status reports no average happiness

### Requirement: Mood boards with the guest

When a group leaves the queue to board, each member SHALL board with the happiness they have at that moment (after any wait-time decline), their preferred ride intensity and their nausea.

#### Scenario: Boarded passenger keeps the queued mood

- **WHEN** a guest with current happiness 40, preferred intensity 90 and nausea 0 is boarded from the queue
- **THEN** the passenger in that guest's seat has happiness 40, preferred intensity 90 and nausea 0
