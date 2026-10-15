# Spec Delta

## Purpose

Gives every guest a happiness, a preferred ride intensity and a nausea level. Defines how these start, how waiting in the queue and riding change them, and how they are summarised for the queue and the ride.

## ADDED Requirements

### Requirement: Guests carry three experience attributes on a 0–100 scale

Every guest SHALL have a happiness, a preferred ride intensity and a nausea level, each a number from 0 (lowest) to 100 (highest). Happiness 0 means very sad and 100 means extremely happy. Intensity 0 means very tame and 100 means the maximum allowed G-force. Nausea 0 means not nauseous at all. No attribute SHALL ever go below 0 or above 100.

#### Scenario: Attributes stay within range

- **WHEN** any rule would push an attribute below 0 or above 100
- **THEN** the attribute is held at 0 or 100 respectively

#### Scenario: Out-of-range values are rejected on creation

- **WHEN** a guest is created with a happiness, preferred intensity or nausea outside 0–100
- **THEN** creation is rejected with a domain validation error

### Requirement: Guests arrive with randomised happiness and preference and no nausea

When a guest is generated on arrival at the queue, their happiness SHALL be drawn uniformly at random from 65 to 85 inclusive, their preferred ride intensity uniformly at random from 50 to 100 inclusive, and their nausea SHALL be 0. The draws SHALL come from the configurable seeded random source, so the same seed yields the same guests.

#### Scenario: Starting values fall in their ranges

- **WHEN** a large number of guests are generated
- **THEN** every guest's happiness is between 65 and 85, every preferred intensity is between 50 and 100, and every nausea is 0

#### Scenario: Seeded generation is reproducible

- **WHEN** two generators are configured with the same seed and each generates the same number of guests
- **THEN** both produce identical happiness and preferred intensity values in the same order

### Requirement: Queue records when each group joined

The queue SHALL record the moment each group was enqueued, taken from the system's time source, so that each member's waiting time can be determined.

#### Scenario: Enqueue time is recorded

- **WHEN** a group is enqueued at time T
- **THEN** the queue reports T as that group's enqueue time

### Requirement: Happiness decays exponentially after five minutes of waiting

A queued guest's happiness SHALL stay at its starting value for the first 5 minutes of waiting. After that it SHALL decay exponentially with the waiting time beyond 5 minutes, falling by half every configured half-life (default 10 minutes). The grace period and half-life SHALL be configurable. Decay SHALL depend only on the starting value and the elapsed waiting time.

#### Scenario: No decay within the grace period

- **WHEN** a guest who arrived with happiness 80 has waited 4 minutes
- **THEN** their happiness is 80

#### Scenario: Exponential decay after the grace period

- **WHEN** a guest who arrived with happiness 80 has waited 15 minutes, with the default 5-minute grace period and 10-minute half-life
- **THEN** their happiness is 40

#### Scenario: Happiness keeps falling while waiting

- **WHEN** the same guest's happiness is read at 6, 10 and 25 minutes of waiting
- **THEN** each reading is lower than the previous one

### Requirement: Experience attributes carry over from the queue onto the ride

When a group is boarded, each member SHALL be seated with their happiness as it stands at the moment of boarding, including any queue decay, together with their preferred ride intensity and nausea.

#### Scenario: Decayed happiness is boarded

- **WHEN** a guest whose queue happiness has decayed to 60 is boarded
- **THEN** the seated passenger has happiness 60 and their original preferred intensity and nausea

### Requirement: Ride intensity is felt G-force as a percentage of the maximum allowed G-force

A gondola's ride intensity SHALL be its felt G-force divided by the maximum allowed G-force (4.5 g), expressed as a percentage and clamped to 0–100. Felt G-force is the magnitude of the gondola's forward and lateral G.

#### Scenario: Half of maximum G

- **WHEN** a gondola's felt G-force is 2.25 g
- **THEN** its ride intensity is 50

#### Scenario: Above maximum G

- **WHEN** a gondola's felt G-force is 5.0 g
- **THEN** its ride intensity is 100

### Requirement: Riders grow happier when the intensity matches their preference

While seated in a gondola, a passenger's happiness SHALL rise over time whenever the gondola's ride intensity is within 15 points of the passenger's preferred intensity. The closer the match, the faster the rise, with the fastest rise at an exact match. Outside that band, the ride intensity SHALL NOT change the passenger's happiness.

#### Scenario: A tame-preferring rider enjoys a moderate ride

- **WHEN** a passenger preferring 50 rides for several seconds at intensity 50
- **THEN** their happiness increases significantly

#### Scenario: A thrill-seeker enjoys maximum G

- **WHEN** a passenger preferring 100 rides for several seconds at intensity 100
- **THEN** their happiness increases significantly

#### Scenario: A closer match raises happiness faster

- **WHEN** two passengers with equal starting happiness ride for the same duration, one at an exact match and one 10 points off their preference
- **THEN** the exactly matched passenger ends with the higher happiness

#### Scenario: A mismatched ride does not raise happiness

- **WHEN** a passenger preferring 100 rides at intensity 40
- **THEN** their happiness does not increase

### Requirement: Riders become nauseous exponentially when the ride is too intense for them

While seated, a passenger's nausea SHALL grow exponentially over time whenever the gondola's ride intensity exceeds the passenger's preferred intensity by 30 points or more. Growth SHALL start even from a nausea of 0. Below that excess, ride intensity SHALL NOT increase nausea.

#### Scenario: A low-preference rider on an intense ride

- **WHEN** a passenger preferring 50 rides at intensity 85 for a sustained period
- **THEN** their nausea rises from 0, and each equal interval adds more nausea than the one before

#### Scenario: Excess below the threshold

- **WHEN** a passenger preferring 50 rides at intensity 75
- **THEN** their nausea does not increase from ride intensity

### Requirement: Sustained maximum G adds a nausea penalty per episode

When a gondola's felt G-force stays at or above the maximum allowed G-force for more than 1 second, each passenger in that gondola SHALL gain 25 nausea points. This SHALL happen once per episode. A new penalty SHALL require the G-force to fall below the maximum and then again stay at or above it for more than 1 second.

#### Scenario: One sustained episode

- **WHEN** a gondola stays at or above maximum G for 3 continuous seconds
- **THEN** each of its passengers gains exactly 25 nausea points from that episode

#### Scenario: Too short to count

- **WHEN** a gondola is at or above maximum G for 0.5 seconds and then drops below it
- **THEN** its passengers gain no over-G nausea

#### Scenario: Repeated episodes accumulate

- **WHEN** a gondola has two separate episodes, each above maximum G for more than 1 second, with a dip below maximum G between them
- **THEN** each of its passengers gains 25 nausea points per episode, 50 in total

### Requirement: Experience dynamics are deterministic

Changes to riders' experience SHALL be computed in the fixed-timestep simulation tick. Changes to queued guests' happiness SHALL be computed from the time source. Given the same seed, inputs and elapsed time, the system SHALL produce identical experience values.

#### Scenario: Replayed simulation

- **WHEN** the same seeded simulation is run twice with identical operator inputs
- **THEN** every passenger's happiness and nausea are identical at each tick in both runs

### Requirement: Queue status reports guests' experience and the average happiness

The queue status SHALL report each waiting guest's current happiness, preferred ride intensity and nausea, and the average current happiness of all waiting guests. When the queue is empty, the average SHALL be reported as absent rather than 0.

#### Scenario: Average over waiting guests

- **WHEN** three guests are waiting with current happiness 60, 70 and 80
- **THEN** the queue status reports an average happiness of 70

#### Scenario: Empty queue

- **WHEN** no guests are waiting
- **THEN** the queue status reports no average happiness
