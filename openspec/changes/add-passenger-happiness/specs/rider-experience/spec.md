# Spec Delta

## Purpose

Defines how a passenger's mood evolves on the ride. Riders get happier when the ride's intensity matches their preference and nauseous when it far exceeds it or holds the maximum allowed G-force. The ride reports a ride-wide summary of its riders' mood.

## ADDED Requirements

### Requirement: Riders board with the mood they had in the queue

A guest boarded from the queue SHALL become a passenger with the happiness, preferred ride intensity and nausea they had when their group left the queue. A passenger boarded without a known mood SHALL get happiness 75, preferred intensity 75 and nausea 0.

#### Scenario: Queue mood is carried aboard

- **WHEN** a group member with happiness 40, preferred intensity 90 and nausea 0 is boarded
- **THEN** the passenger in their seat reports happiness 40, preferred intensity 90 and nausea 0

#### Scenario: Passenger boarded by weight only

- **WHEN** a single passenger is boarded with only a weight
- **THEN** the passenger reports happiness 75, preferred intensity 75 and nausea 0

### Requirement: Ride intensity is felt G as a percentage of the maximum allowed G

Each gondola's ride intensity SHALL be the total G-force its riders feel (horizontal force combined with 1 g of gravity) as a percentage of the maximum allowed G-force of 4.5 g. It is capped at 100.

#### Scenario: Half the maximum G is 50 % intensity

- **WHEN** a gondola's riders feel 2.25 g
- **THEN** the gondola's ride intensity is 50

#### Scenario: Intensity is capped

- **WHEN** a gondola's riders feel 5.0 g
- **THEN** the gondola's ride intensity is 100

### Requirement: Riders get happier when the intensity matches their preference

While the ride is in motion, a rider's happiness SHALL rise when their gondola's intensity is within 15 percentage points of their preferred intensity. The gain is fastest at an exact match and fades to none at the band's edges. Happiness SHALL never exceed 100.

#### Scenario: A 50 %-rider enjoys a medium ride

- **WHEN** a rider who prefers 50 % rides for 10 seconds at 50 % intensity
- **THEN** their happiness has risen markedly (by about 10 points)

#### Scenario: A 100 %-rider enjoys a ride at the limit

- **WHEN** a rider who prefers 100 % rides at an intensity of 100 %
- **THEN** their happiness rises, and it rises faster than for the same rider at 90 % intensity

#### Scenario: A mismatched ride brings no joy

- **WHEN** a rider who prefers 100 % rides at 50 % intensity
- **THEN** their happiness does not change

#### Scenario: Happiness is capped

- **WHEN** a rider with happiness 99.5 rides at their exact preferred intensity for 10 seconds
- **THEN** their happiness is 100

### Requirement: Over-intensity makes riders nauseous exponentially

While the ride is in motion and a gondola's intensity exceeds a rider's preference by 30 percentage points or more, that rider's nausea SHALL grow exponentially, doubling about every 10 seconds from a near-zero start. Nausea SHALL never exceed 100. Below that threshold, nausea SHALL not change.

#### Scenario: Prolonged over-intensity escalates nausea

- **WHEN** a rider who prefers 50 % with nausea 0 rides at 85 % intensity for 20, 40 and 60 seconds
- **THEN** their nausea is about 3, then about 15, then about 63

#### Scenario: Below the threshold nausea holds

- **WHEN** a rider who prefers 50 % with nausea 10 rides at 75 % intensity
- **THEN** their nausea stays 10

### Requirement: Sustained maximum G gives riders a nausea shock

Each time a gondola's riders feel the maximum allowed G-force or more for longer than one second without interruption, every passenger in that gondola SHALL gain 25 nausea, capped at 100. A further shock requires the G-force to fall below the maximum and then hold it again for more than one second.

#### Scenario: One sustained episode, one shock

- **WHEN** a gondola holds at or above 4.5 g for 3 continuous seconds
- **THEN** each of its passengers gains exactly 25 nausea from the over-G shock

#### Scenario: A brief peak is not a shock

- **WHEN** a gondola touches 4.5 g for half a second and then drops below it
- **THEN** its passengers gain no over-G shock nausea

#### Scenario: Two episodes, two shocks

- **WHEN** a gondola holds above 4.5 g for 1.5 seconds, drops below it, then holds above it again for 1.5 seconds
- **THEN** each of its passengers has gained 50 nausea from over-G shocks

### Requirement: Mood only changes while the ride is in motion

Rider happiness and nausea SHALL change only while the ride is running, stopping or in an emergency stop. They SHALL stay unchanged while the ride is idle, loading, safe or offloading.

#### Scenario: Loading leaves mood unchanged

- **WHEN** the ride spends 60 seconds in the loading state with passengers aboard
- **THEN** every passenger's happiness and nausea are unchanged

### Requirement: Mood evolution is deterministic

Given the same starting state and the same sequence of simulation steps and operator commands, rider mood SHALL evolve identically.

#### Scenario: Replaying a ride reproduces mood

- **WHEN** two identical rides with identical passengers run the same command sequence for the same number of steps
- **THEN** every passenger's happiness and nausea are identical between the two rides

### Requirement: Ride telemetry reports a rider-experience summary

Ride telemetry SHALL include the number of passengers aboard and their average happiness, average preferred ride intensity and average nausea. When nobody is aboard, the three averages SHALL be reported as absent rather than 0.

#### Scenario: Summary over boarded riders

- **WHEN** two passengers are aboard with happiness 60 and 80, preferences 50 and 100, and nausea 0 and 20
- **THEN** telemetry reports 2 riders, average happiness 70, average preferred intensity 75 and average nausea 10

#### Scenario: Empty ride has no averages

- **WHEN** no passengers are aboard
- **THEN** telemetry reports 0 riders and no averages
