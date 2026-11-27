# Spec Delta

## Purpose

Defines how a passenger's mood changes while riding: how intense each gondola feels, how a ride that matches a rider's preference makes them happier, how a ride that is too intense makes them nauseous, and the ride-wide summary the operator sees.

## ADDED Requirements

### Requirement: Each gondola has a ride intensity relative to the maximum allowed G-force

Each gondola SHALL have a ride intensity of 0–100, equal to its felt G-force as a percentage of the maximum allowed G-force (4.5 g), capped at 100. The felt G-force SHALL combine the gondola's forward and lateral G with 1 g of gravity. A gondola at rest therefore has an intensity of about 22.

#### Scenario: Half of the allowed G-force reads as 50 %

- **WHEN** a gondola's felt G-force is 2.25 g
- **THEN** its ride intensity is 50

#### Scenario: Intensity is capped at 100

- **WHEN** a gondola's felt G-force is 5.0 g
- **THEN** its ride intensity is 100

### Requirement: Riders get happier when the intensity matches their preference

While the ride's physics is running, a passenger SHALL gain happiness at up to 1 point per second. The full rate SHALL apply when their gondola's intensity equals their preferred intensity, falling linearly to no gain at a difference of 15 or more percentage points in either direction. A mismatched intensity SHALL NOT reduce happiness. Happiness SHALL never exceed 100.

#### Scenario: Gentle rider is happiest on a moderate ride

- **WHEN** a passenger who prefers intensity 50 rides for 10 seconds in a gondola held at intensity 50
- **THEN** their happiness rises by 10 points

#### Scenario: Thrill-seeker is happiest at the G-force limit

- **WHEN** a passenger who prefers intensity 100 rides for 10 seconds in a gondola held at intensity 100
- **THEN** their happiness rises by 10 points

#### Scenario: Partial match gives partial gain

- **WHEN** a passenger who prefers intensity 60 rides for 10 seconds in a gondola held at intensity 52.5
- **THEN** their happiness rises by 5 points

#### Scenario: Far-off intensity gives no gain and no loss

- **WHEN** a passenger who prefers intensity 100 rides for 10 seconds in a gondola held at intensity 50
- **THEN** their happiness is unchanged

### Requirement: Riders become nauseous exponentially when the ride is far too intense

While the ride's physics is running, if a passenger's gondola intensity exceeds their preferred intensity by 30 or more percentage points, their nausea SHALL grow exponentially, doubling (offset by one point) every 10 seconds. Starting from 0, it reaches about 3 after 20 s, 15 after 40 s and 63 after 60 s. Below that gap nausea SHALL stay as it is. Nausea SHALL never exceed 100.

#### Scenario: Gentle rider on an intense ride gets sick

- **WHEN** a passenger with nausea 0 who prefers intensity 50 rides for 40 seconds in a gondola held at intensity 80
- **THEN** their nausea is 15 (within rounding)

#### Scenario: A gap under 30 points causes no nausea

- **WHEN** a passenger who prefers intensity 50 rides for 60 seconds in a gondola held at intensity 79
- **THEN** their nausea is unchanged

#### Scenario: A ride below preference causes no nausea

- **WHEN** a passenger who prefers intensity 100 rides in a gondola at intensity 22
- **THEN** their nausea is unchanged

### Requirement: Sustained maximum G-force shocks every passenger in the gondola

Each time a gondola's felt G-force stays at or above the maximum allowed G-force (4.5 g) for more than 1 second without a break, every passenger in that gondola SHALL gain 25 nausea points, capped at 100. The shock SHALL fire once per uninterrupted episode. A new episode starts only after the G-force has dropped below the maximum.

#### Scenario: One sustained episode adds 25 nausea

- **WHEN** a gondola carrying passengers with nausea 10 is held at 4.6 g for 3 seconds
- **THEN** each of those passengers has nausea 35

#### Scenario: A brief spike does not shock

- **WHEN** a gondola is at 4.6 g for 0.9 seconds and then drops to 3 g
- **THEN** its passengers' nausea is unchanged by the spike

#### Scenario: Every new episode shocks again

- **WHEN** a gondola goes through two separate episodes above 4.5 g, each longer than 1 second, with a drop below 4.5 g between them
- **THEN** its passengers gain 50 nausea points in total

### Requirement: Mood changes only while the ride's physics runs

Riding mood SHALL change only while the ride's physics runs (Started, Stopping and EmergencyStop). It SHALL NOT change during loading, offloading or while the ride is idle. Mood updates SHALL advance on the simulation's fixed time step, so they are as deterministic as the rest of the twin.

#### Scenario: No mood change while loading

- **WHEN** passengers sit in their seats while the ride is Loading
- **THEN** their happiness and nausea do not change

### Requirement: Ride telemetry carries a rider-experience summary

Every ride telemetry snapshot SHALL include a rider-experience summary with the number of passengers on the ride and their average happiness, average preferred ride intensity and average nausea. When nobody is on the ride, the count SHALL be 0 and each average SHALL be absent rather than 0.

#### Scenario: Averages over boarded passengers

- **WHEN** two passengers are on the ride, with happiness 60 and 80, preferred intensity 50 and 70, and nausea 0 and 20
- **THEN** the summary reports 2 riders, average happiness 70, average preferred intensity 60 and average nausea 10

#### Scenario: Empty ride has no averages

- **WHEN** no passengers are on the ride
- **THEN** the summary reports 0 riders and no average values

### Requirement: A seat-only boarding without a guest uses a neutral mood

A passenger boarded directly into a seat, without coming from the queue, SHALL board with happiness 75, preferred intensity 75 and nausea 0.

#### Scenario: Directly boarded passenger

- **WHEN** an operator boards a passenger into a seat by weight alone
- **THEN** that passenger has happiness 75, preferred intensity 75 and nausea 0
