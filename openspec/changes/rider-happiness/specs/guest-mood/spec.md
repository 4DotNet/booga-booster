# Spec Delta

## Purpose

Gives every guest a mood made of happiness, preferred ride intensity and nausea. It defines how the mood starts when a guest joins the queue, how waiting erodes it, and how it is reported and carried onto the ride.

## ADDED Requirements

### Requirement: Every guest has three bounded mood scores

Every person SHALL carry a happiness, a preferred ride intensity and a nausea score. Each score SHALL lie within 0–100 inclusive, where 0 is the lowest (very sad, gentlest preferred ride, no nausea) and 100 is the highest (extremely happy, most intense preferred ride, extremely nauseous). A score outside this range SHALL be rejected as a domain validation error.

#### Scenario: Scores within range are accepted

- **WHEN** a person is created with happiness 70, preferred intensity 90 and nausea 0
- **THEN** the person carries exactly those three scores

#### Scenario: Out-of-range score is rejected

- **WHEN** a person is created with a happiness of 101 or a preferred intensity of -1
- **THEN** creation fails with a domain validation error

### Requirement: Initial mood is drawn when a guest joins the queue

When a person is generated for the queue, their happiness SHALL be a whole number drawn uniformly from 65 to 85 inclusive, their preferred ride intensity a whole number drawn uniformly from 50 to 100 inclusive, and their nausea SHALL be 0. The draws SHALL use the queue's seeded randomness so that a fixed seed reproduces the same moods.

#### Scenario: New guests start fairly happy and unnauseated

- **WHEN** 1,000 people are generated
- **THEN** every happiness lies within 65–85, every preferred intensity lies within 50–100, and every nausea is 0

#### Scenario: Seeded generation is reproducible

- **WHEN** two generators with the same random seed each generate 10 people
- **THEN** both sequences have identical happiness and preferred intensity values

### Requirement: Happiness declines exponentially after five minutes of waiting

A guest's happiness SHALL stay at its arrival value for the first 5 minutes in the queue. After that it SHALL decline at an accelerating, exponential rate as the wait grows: the loss SHALL be 5 points at 10 minutes, 15 at 15 minutes, 35 at 20 minutes and 75 at 25 minutes. Happiness SHALL never fall below 0. Waiting SHALL NOT change preferred intensity or nausea.

#### Scenario: No decline within the patience window

- **WHEN** a guest who arrived with happiness 75 has waited 4 minutes 59 seconds
- **THEN** their happiness is 75

#### Scenario: Decline accelerates with the wait

- **WHEN** a guest who arrived with happiness 75 has waited 10, 15 and then 20 minutes
- **THEN** their happiness is 70, 60 and then 40 respectively

#### Scenario: Happiness bottoms out at zero

- **WHEN** a guest who arrived with happiness 65 has waited 30 minutes
- **THEN** their happiness is 0

### Requirement: Queue status reports each guest's current mood and the average happiness

The queue status SHALL report, for every waiting person, their current happiness (wait decline applied), preferred ride intensity and nausea. It SHALL also report the average current happiness of everyone waiting, or no value when the queue is empty. These fields SHALL be additive to the existing status contract.

#### Scenario: Average over waiting guests

- **WHEN** the queue holds three guests whose current happiness is 60, 70 and 80
- **THEN** the queue status reports an average happiness of 70

#### Scenario: Empty queue has no average

- **WHEN** the queue holds nobody
- **THEN** the queue status reports no average happiness rather than 0

### Requirement: A guest's mood boards the ride with them

When a group leaves the queue to board, each member SHALL board with the happiness they had at the moment they left the queue, together with their preferred ride intensity and nausea.

#### Scenario: Mood survives boarding

- **WHEN** a guest who arrived with happiness 80 and preferred intensity 60 leaves the queue after a 15-minute wait and boards
- **THEN** that passenger is on the ride with happiness 65, preferred intensity 60 and nausea 0
