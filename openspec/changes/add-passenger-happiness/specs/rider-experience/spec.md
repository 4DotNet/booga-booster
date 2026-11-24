# Spec Delta

## Purpose

Defines how a passenger's mood changes while the ride runs: how ride intensity is measured, how a matching intensity makes a rider happier, how too much intensity and sustained over-G make them nauseous, and the ride-wide summary of rider mood.

## ADDED Requirements

### Requirement: Ride intensity is felt G as a share of the maximum allowed G

Each gondola SHALL have a ride intensity: the total G-force its riders feel (horizontal load combined with 1 g of gravity) as a percentage of the maximum allowed G-force of 4.5 g. The value SHALL be capped at 100. Every passenger in a gondola SHALL experience that gondola's intensity.

#### Scenario: Half the maximum G

- **WHEN** a gondola's riders feel 2.25 g in total
- **THEN** the gondola's ride intensity is 50

#### Scenario: Above the maximum G

- **WHEN** a gondola's riders feel 5.0 g in total
- **THEN** the gondola's ride intensity is 100

#### Scenario: Ride at rest

- **WHEN** a gondola's riders feel only gravity (1.0 g)
- **THEN** the gondola's ride intensity is about 22

### Requirement: A matching intensity makes riders happier

While the ride runs, a rider's happiness SHALL rise at up to 1 point per second. The full rate applies when the gondola's intensity equals the rider's preferred intensity. The rate SHALL fall linearly to zero as the two differ by 15 percentage points, and be zero beyond that. Happiness SHALL never exceed 100.

#### Scenario: Moderate rider on a moderate ride

- **WHEN** a rider who prefers 50 % rides for 10 seconds at an intensity of 50
- **THEN** their happiness has risen by 10 points

#### Scenario: Thrill-seeker at the maximum G

- **WHEN** a rider who prefers 100 % rides for 10 seconds at an intensity of 100
- **THEN** their happiness has risen by 10 points

#### Scenario: Half-matched intensity gives half the gain

- **WHEN** a rider who prefers 80 % rides for 10 seconds at an intensity of 72.5
- **THEN** their happiness has risen by 5 points

#### Scenario: Mismatched intensity gives no gain

- **WHEN** a rider who prefers 100 % rides for 10 seconds at an intensity of 50
- **THEN** their happiness is unchanged

#### Scenario: Happiness is capped

- **WHEN** a rider with happiness 95 rides for 10 seconds at their preferred intensity
- **THEN** their happiness is 100

### Requirement: Too much intensity makes riders nauseous exponentially

While the ride runs and a gondola's intensity is at least 30 percentage points above a rider's preferred intensity, that rider's nausea SHALL grow exponentially, slowly at first and then faster: nausea plus 1 doubles every 10 seconds. Below that gap, nausea SHALL stay as it is; it does not recover on the ride. Nausea SHALL never exceed 100.

#### Scenario: Nausea builds and then climbs steeply

- **WHEN** a rider with nausea 0 who prefers 50 % rides at an intensity of 85 for 20, 40 and then 60 seconds
- **THEN** their nausea is about 3, about 15 and about 63 respectively

#### Scenario: A gap under 30 points causes no nausea

- **WHEN** a rider with nausea 0 who prefers 50 % rides at an intensity of 79 for 60 seconds
- **THEN** their nausea is still 0

#### Scenario: Nausea is capped

- **WHEN** a rider who prefers 50 % rides at an intensity of 100 for 120 seconds
- **THEN** their nausea is 100

### Requirement: Sustained maximum G shocks riders with nausea

Each time a gondola's felt G-force stays at or above the maximum allowed G-force (4.5 g) for longer than 1 second, every passenger in it SHALL gain 25 nausea points, capped at 100. One unbroken stretch at or above the maximum SHALL count once, however long it lasts. The next shock requires the G-force to drop below the maximum and stay at or above it for longer than 1 second again.

#### Scenario: One sustained episode

- **WHEN** a gondola carrying a rider with nausea 10 stays at or above 4.5 g for 3 seconds without a break
- **THEN** that rider's nausea rises by exactly 25 from the shock

#### Scenario: A brief peak causes no shock

- **WHEN** a gondola touches 4.5 g for 0.5 seconds and then drops below it
- **THEN** its riders gain no shock nausea

#### Scenario: Two separate episodes

- **WHEN** a gondola stays at or above 4.5 g for 1.5 seconds, drops below it, and then stays at or above it for 1.5 seconds again
- **THEN** its riders gain 25 shock nausea twice

### Requirement: Mood changes only while the ride is in motion

Rider happiness and nausea SHALL change only while the ride is started, stopping or in an emergency stop. They SHALL stay fixed while the ride is idle, loading, safe to start or offloading. Updates SHALL follow the simulation's fixed timestep, so replaying the same commands from the same state gives the same moods.

#### Scenario: Loading leaves mood unchanged

- **WHEN** a seated rider waits 60 seconds while the ride is loading
- **THEN** their happiness and nausea are unchanged

#### Scenario: Deterministic replay

- **WHEN** two identical rides with identical riders receive the same commands for the same simulated time
- **THEN** every rider's happiness and nausea are identical between the two rides

### Requirement: Ride-wide rider experience summary

The ride SHALL report a rider-experience summary: how many passengers are on the ride, and their average happiness, average preferred intensity and average nausea. The averages SHALL include every seated passenger in any ride state. When nobody is aboard, the averages SHALL be reported as absent, not as 0.

#### Scenario: Averages across riders

- **WHEN** three passengers are aboard with happiness 60, 80 and 70, preferred intensity 50, 100 and 75, and nausea 0, 20 and 10
- **THEN** the summary reports 3 riders, average happiness 70, average preferred intensity 75 and average nausea 10

#### Scenario: Empty ride

- **WHEN** nobody is aboard
- **THEN** the summary reports 0 riders and no averages
