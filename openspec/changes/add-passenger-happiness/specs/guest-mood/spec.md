# Spec Delta

## Purpose

Gives every guest a mood: happiness, preferred ride intensity and nausea. It defines how that mood is initialised when the guest arrives, how waiting in the queue erodes happiness, and how the queue reports its guests' mood.

## ADDED Requirements

### Requirement: Guests carry three mood scores on a 0–100 scale

Every guest SHALL carry a happiness score, a preferred-ride-intensity score and a nausea score. Each is a number from 0 (lowest) to 100 (highest): 0 happiness is very sad and 100 is extremely happy. A value outside [0, 100] SHALL be rejected as a domain validation error.

#### Scenario: Valid mood is accepted

- **WHEN** a guest is created with happiness 70, preferred intensity 90 and nausea 0
- **THEN** the guest reports exactly those three scores

#### Scenario: Out-of-range score is rejected

- **WHEN** a guest is created with happiness 101, or with any score below 0
- **THEN** creation fails with a domain validation error and no guest is produced

### Requirement: New guests arrive fairly happy with a random preference and no nausea

A newly generated guest SHALL start with happiness drawn uniformly from 65 to 85 inclusive, preferred ride intensity drawn uniformly from 50 to 100 inclusive, and nausea 0. The draws SHALL use the queue's seeded randomness so a fixed seed reproduces the same guests.

#### Scenario: Initial mood is within range

- **WHEN** 1,000 guests are generated
- **THEN** every guest's happiness is between 65 and 85, every preferred intensity is between 50 and 100, and every nausea is 0

#### Scenario: Initial mood is reproducible

- **WHEN** two generators with the same seed each generate 10 guests
- **THEN** both sequences have identical happiness and preferred-intensity values

### Requirement: Happiness holds for the first five minutes in the queue

A guest's happiness SHALL stay at its arrival value while the guest has waited five minutes or less, measured from the moment their group joined the queue.

#### Scenario: Short wait does not affect happiness

- **WHEN** a guest who arrived with happiness 80 has waited 4 minutes 59 seconds
- **THEN** the guest's happiness is 80

### Requirement: Happiness decays exponentially after five minutes in the queue

Once a guest has waited longer than five minutes, their happiness SHALL decrease exponentially with the extra wait. It halves for every further ten minutes, never drops below 0, and is never restored while they keep waiting.

#### Scenario: Happiness halves after ten extra minutes

- **WHEN** a guest who arrived with happiness 80 has waited 15 minutes
- **THEN** the guest's happiness is 40

#### Scenario: The longer the wait, the lower the happiness

- **WHEN** the same guest is observed after 10, 20 and 30 minutes of waiting
- **THEN** each observation reports a strictly lower happiness than the previous one

### Requirement: Queued guests expose their current mood

The queue status and a group taken from the queue for boarding SHALL report each guest's happiness as it stands at that moment, after any waiting decay. They SHALL also report the guest's preferred ride intensity and nausea.

#### Scenario: A boarded group carries its decayed happiness

- **WHEN** a group whose member arrived with happiness 80 is taken for boarding after 15 minutes in the queue
- **THEN** the returned member reports happiness 40, along with their preferred intensity and nausea

### Requirement: Queue status reports average happiness

The queue status SHALL report the average current happiness of all guests waiting in the queue. When the queue is empty, the average SHALL be reported as absent rather than 0.

#### Scenario: Average over waiting guests

- **WHEN** three guests with current happiness 60, 70 and 80 are waiting
- **THEN** the queue status reports an average happiness of 70

#### Scenario: Empty queue has no average

- **WHEN** nobody is waiting in the queue
- **THEN** the queue status reports no average happiness
