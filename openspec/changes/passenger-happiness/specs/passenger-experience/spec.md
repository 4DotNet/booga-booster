# Spec Delta

## Purpose

Gives every guest a happiness, preferred ride intensity and nausea rating, and defines how waiting in the queue and the ride's felt G-forces change those ratings, so the twin can show how well the ride is operated for the people on it.

## ADDED Requirements

### Requirement: Guests carry three bounded experience ratings

Every guest SHALL carry a happiness, a preferred ride intensity and a nausea rating. Each rating SHALL be a real number in the closed range 0 to 100, where 0 is the lowest (very sad, tamest, not nauseous) and 100 the highest (extremely happy, most intense, maximally nauseous). Any change that would move a rating outside that range SHALL clamp it to the nearest bound; constructing a guest with an out-of-range rating SHALL be rejected as a validation error.

#### Scenario: Ratings stay within range

- **WHEN** an update would raise a guest's nausea from 90 by 25
- **THEN** the guest's nausea is 100

#### Scenario: Out-of-range rating is rejected

- **WHEN** a guest is constructed with a happiness of 120
- **THEN** construction fails with a validation error

### Requirement: Initial ratings on arrival

A guest generated on arrival at the queue SHALL start with a happiness drawn uniformly from [65, 85], a preferred ride intensity drawn uniformly from [50, 100], and a nausea of exactly 0. The draws SHALL use the queue's seedable random source, so the same seed produces the same ratings.

#### Scenario: Freshly generated guests are within their initial ranges

- **WHEN** 1,000 guests are generated
- **THEN** every guest's happiness is in [65, 85], every preferred intensity is in [50, 100], and every nausea is 0

#### Scenario: Seeded generation is reproducible

- **WHEN** two generators with the same seed each generate 10 guests
- **THEN** both sequences have identical happiness and preferred-intensity values

### Requirement: Waiting longer than five minutes erodes happiness exponentially

A waiting guest's happiness SHALL be unchanged for the first 5 minutes after their group joined the queue. Beyond 5 minutes, happiness SHALL decay exponentially with the waiting time past 5 minutes: `H(w) = H₀ · e^(−(w − 5 min)/τ)`, where `H₀` is the happiness on arrival and `τ` is a documented queue-patience time constant. Waited happiness SHALL be a pure function of arrival happiness and elapsed time.

#### Scenario: No erosion within the grace period

- **WHEN** a guest who arrived with happiness 80 has waited 4 minutes 59 seconds
- **THEN** the guest's happiness is 80

#### Scenario: Erosion is exponential past the grace period

- **WHEN** the same guest's happiness is read at 5, 8 and 11 minutes of waiting
- **THEN** happiness at 8 minutes is lower than at 5 minutes, and the ratio between the 11-minute and 8-minute values equals the ratio between the 8-minute and 5-minute values

#### Scenario: Erosion follows simulated time

- **WHEN** the queue's time source is advanced by 10 minutes without any other activity
- **THEN** the reported happiness of a guest who joined at the start reflects 10 minutes of waiting

### Requirement: Ratings follow the guest onto the ride

When a group boards, each member SHALL become a rider carrying their preferred intensity, their nausea and their happiness as eroded by the time they waited in the queue up to the moment they were taken.

#### Scenario: Waited happiness carries onto the ride

- **WHEN** a guest who arrived with happiness 80 is taken from the queue after waiting long enough for their happiness to erode to 60
- **THEN** the boarded rider has happiness 60, the same preferred intensity, and nausea 0

#### Scenario: Riders keep their ratings until offloaded

- **WHEN** the ride is loading and not yet running
- **THEN** boarded riders' ratings do not change

### Requirement: Experienced ride intensity per gondola

While the ride's physics is running (started, stopping or emergency stop), each gondola SHALL have an experienced intensity equal to the magnitude of its felt horizontal G-force (the vector combining its forward and lateral G) divided by the maximum allowed G-force (4.5 g), expressed as a percentage and capped at 100. Both riders of a gondola experience that gondola's intensity.

#### Scenario: Half of the maximum G-force

- **WHEN** a gondola feels 2.25 g of combined forward and lateral G-force
- **THEN** its riders experience an intensity of 50

#### Scenario: Beyond the maximum G-force

- **WHEN** a gondola feels 5.0 g
- **THEN** its riders experience an intensity of 100

### Requirement: Riders become happier near their preferred intensity

While the ride's physics is running, each rider's happiness SHALL increase at a rate that is greatest when the experienced intensity equals the rider's preferred intensity and falls off smoothly and symmetrically as the two diverge, becoming negligible when they differ by 30 points or more. A ride's intensity SHALL never decrease a rider's happiness.

#### Scenario: A moderate rider on a moderate ride

- **WHEN** a rider who prefers intensity 50 rides for 10 seconds at an experienced intensity of 50
- **THEN** their happiness rises by more than it would for 10 seconds at an experienced intensity of 80

#### Scenario: A thrill-seeker at maximum G-force

- **WHEN** a rider who prefers intensity 100 rides at an experienced intensity of 100
- **THEN** their happiness rises faster than it does for that rider at an experienced intensity of 50

#### Scenario: A ride at rest does not change happiness

- **WHEN** the ride is in a state in which its physics is not running
- **THEN** no rider's happiness changes

### Requirement: Excess intensity makes riders nauseous exponentially

While the ride's physics is running and a rider's experienced intensity exceeds their preferred intensity by 30 points or more, that rider's nausea SHALL grow exponentially over the time the condition persists, starting from a documented base growth rate even at zero nausea. Below that 30-point excess, ride intensity SHALL NOT change nausea. Nausea SHALL never decrease while riding.

#### Scenario: Below the excess threshold

- **WHEN** a rider who prefers intensity 50 experiences an intensity of 79
- **THEN** their nausea does not change

#### Scenario: Sustained excess accelerates

- **WHEN** a rider who prefers intensity 50 experiences an intensity of 90 for two consecutive equal intervals
- **THEN** their nausea increases during both intervals, and by more in the second interval than in the first

### Requirement: Sustained maximum G-force adds a fixed nausea penalty

Each time a gondola's felt G-force stays at or above the maximum allowed G-force (4.5 g) continuously for longer than 1 second, each of its riders SHALL gain 25 nausea points, applied once per such episode and clamped at 100. An episode ends when the gondola's G-force falls below the maximum; a new episode then starts the 1-second count from zero.

#### Scenario: One long episode adds the penalty once

- **WHEN** a gondola stays at or above 4.5 g for 3 continuous seconds
- **THEN** each of its riders gains exactly 25 nausea points from this rule

#### Scenario: Brief peaks add nothing

- **WHEN** a gondola reaches 4.5 g for 0.8 seconds and then drops below it
- **THEN** its riders gain no nausea from this rule

#### Scenario: Separate episodes each add the penalty

- **WHEN** a gondola has two separate episodes above 4.5 g, each lasting longer than 1 second, with a drop below 4.5 g in between
- **THEN** each of its riders gains 25 nausea points for each episode, 50 in total from this rule

#### Scenario: Empty seats are unaffected

- **WHEN** a gondola with one occupied seat has a sustained maximum G episode
- **THEN** only the seated rider gains nausea
