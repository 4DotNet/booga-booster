## ADDED Requirements

### Requirement: Every person carries a validated rider profile

Every person SHALL carry a rider profile consisting of a preferred intensity, a happiness value and a nausea rating. The preferred intensity SHALL be a number in `[0.1, 1]`; happiness and nausea SHALL each be a number in `[0, 1]`, where `1` is very happy (respectively maximally nauseous) and `0` is extremely unhappy (respectively not nauseous at all). A profile with any value outside its range, or a non-finite value, SHALL be rejected as a domain validation error.

#### Scenario: A valid profile is accepted

- **WHEN** a person is created with preferred intensity `0.6`, happiness `0.7` and nausea `0`
- **THEN** the person reports exactly those three values

#### Scenario: Preferred intensity below the floor is rejected

- **WHEN** a person is created with preferred intensity `0.05`
- **THEN** a domain validation error is raised and no person is created

#### Scenario: Happiness above the ceiling is rejected

- **WHEN** a person is created with happiness `1.2`
- **THEN** a domain validation error is raised and no person is created

#### Scenario: A non-finite value is rejected

- **WHEN** a person is created with a nausea rating of `NaN`
- **THEN** a domain validation error is raised and no person is created

### Requirement: Arriving guests get a randomly drawn profile

When the person generator creates a guest it SHALL draw the preferred intensity uniformly from `[0.1, 1]`, draw the initial happiness uniformly from `[0.65, 0.85]` and set the nausea rating to `0`. The draws SHALL come from the generator's seeded randomness so that a fixed seed reproduces the same profiles.

#### Scenario: Generated profiles fall within the arrival ranges

- **WHEN** the generator creates one thousand guests
- **THEN** every preferred intensity lies in `[0.1, 1]`
- **AND** every happiness lies in `[0.65, 0.85]`
- **AND** every nausea rating is `0`

#### Scenario: A seed reproduces the profiles

- **WHEN** two generators are created with the same random seed and each creates ten guests
- **THEN** the ten profiles from the first generator equal the ten from the second, in order

### Requirement: Waiting longer than the grumpiness onset erodes happiness

Each queued group SHALL record the moment it joined the line. A person's current happiness SHALL equal their initial happiness for as long as their group has waited no longer than the grumpiness onset (default five minutes). Beyond the onset, current happiness SHALL decrease linearly at the grumpiness rate (default `0.01` per minute) for every minute waited past the onset, and SHALL never fall below `0`. The onset and the rate SHALL be configurable through the Queue module options. The stored initial happiness SHALL NOT be mutated by waiting.

#### Scenario: Happiness is unchanged before the onset

- **WHEN** a person with initial happiness `0.8` has waited four minutes
- **THEN** their current happiness is `0.8`

#### Scenario: Happiness is unchanged exactly at the onset

- **WHEN** a person with initial happiness `0.8` has waited exactly five minutes
- **THEN** their current happiness is `0.8`

#### Scenario: Happiness decreases past the onset

- **WHEN** a person with initial happiness `0.8` has waited fifteen minutes with the default rate of `0.01` per minute
- **THEN** their current happiness is `0.7`

#### Scenario: Happiness never drops below zero

- **WHEN** a person with initial happiness `0.65` has waited long enough that the linear decrease would go negative
- **THEN** their current happiness is `0`

#### Scenario: Waiting does not rewrite the stored value

- **WHEN** a person has waited past the onset
- **THEN** the person's stored initial happiness is unchanged and only the reported current happiness differs

### Requirement: Queue status exposes the mood of the line

The queue status SHALL report, for every waiting person, their preferred intensity, their current (wait-adjusted) happiness and their nausea rating. It SHALL also report the average current happiness of everyone waiting, or no value when the line is empty. A group taken for boarding SHALL be returned with each member's current happiness as of the moment it was taken.

#### Scenario: Queue status carries the profile per person

- **WHEN** the queue status is read while a group is waiting
- **THEN** each person in the response carries a preferred intensity, a happiness and a nausea rating

#### Scenario: Average queue happiness is the mean of current happiness

- **WHEN** two people are waiting with current happiness `0.6` and `0.8`
- **THEN** the queue status reports an average happiness of `0.7`

#### Scenario: Empty queue has no average

- **WHEN** the queue status is read for a ride with nobody waiting
- **THEN** the average happiness is absent (null)

#### Scenario: A taken group carries wait-adjusted happiness

- **WHEN** a group that has waited fifteen minutes is taken for boarding
- **THEN** each returned member's happiness reflects the wait, not their arrival value

### Requirement: Queue status telemetry reports the average mood, never a person

The query that reads the queue status SHALL tag its span with the average happiness of the line. No span or metric SHALL carry an individual person's mood, name or number.

#### Scenario: Span carries the average

- **WHEN** the queue status query completes for a line with people waiting
- **THEN** its activity carries a `queue.happiness.average` attribute equal to the response's average happiness
- **AND** no attribute names an individual person
