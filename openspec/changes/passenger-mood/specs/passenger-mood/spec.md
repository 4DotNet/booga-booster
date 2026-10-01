## ADDED Requirements

### Requirement: Guests are generated with an initial mood

Every generated guest SHALL receive a happiness drawn uniformly from `[65, 85]`, a preferred G drawn uniformly from `[MaxGForce / 2, MaxGForce]` (with `MaxGForce` = 4.5 g, so `[2.25, 4.5]`), and a nausea of `0`. The random draws SHALL come from the queue module's seeded random source so that a fixed seed yields the same guests.

#### Scenario: Initial values lie in range
- **WHEN** a guest is generated
- **THEN** their happiness is between 65 and 85 inclusive, their preferred G is between 2.25 g and 4.5 g inclusive, and their nausea is 0

#### Scenario: Generation is deterministic for a seed
- **WHEN** two person generators are created with the same random seed and each generates the same number of guests
- **THEN** both produce identical happiness and preferred G sequences

### Requirement: Mood values are clamped

Happiness and nausea SHALL always lie within `[0, 100]`. Any rule that would move a value outside that range SHALL leave it at the nearest bound.

#### Scenario: Nausea saturates at 100
- **WHEN** a rider with nausea 90 receives a +25 nausea penalty
- **THEN** their nausea is 100

#### Scenario: Happiness floors at 0
- **WHEN** a queued guest's wait decay exceeds their starting happiness
- **THEN** their reported happiness is 0

### Requirement: Long queue waits decay happiness exponentially

A queued guest's happiness SHALL equal their starting happiness for the first 5 minutes after their group was enqueued. Beyond 5 minutes it SHALL be reduced by `5 × (e^(t / 5 min) − 1)` points, where `t` is the waiting time beyond the first 5 minutes, clamped at 0. The value SHALL be computed from the group's enqueue time and the injected `TimeProvider` whenever the queue is read.

#### Scenario: No decay within five minutes
- **WHEN** a guest with starting happiness 80 has waited 4 minutes
- **THEN** their reported happiness is 80

#### Scenario: Decay after ten minutes
- **WHEN** a guest with starting happiness 80 has waited 10 minutes
- **THEN** their reported happiness is approximately 71.4 (80 − 5 × (e − 1))

#### Scenario: Decay after sixteen minutes makes a guest mad
- **WHEN** a guest with starting happiness 65 has waited 16 minutes
- **THEN** their reported happiness is approximately 24.9 (65 − 5 × (e^2.2 − 1)), below the mad threshold of 30

#### Scenario: Boarding freezes the queue happiness
- **WHEN** a group boards the ride
- **THEN** each member boards with the decayed happiness they had at the moment of boarding

### Requirement: Gondolas report felt G

Each gondola SHALL compute its felt G every physics tick as the magnitude of the specific force including gravity, per `docs/05-forces-and-g-forces.md` §5.1: `√(1 + ForwardG² + LateralG²)` in g.

#### Scenario: A gondola at rest feels one g
- **WHEN** a gondola experiences no horizontal acceleration
- **THEN** its felt G is 1.0

#### Scenario: Horizontal load adds to gravity
- **WHEN** a gondola's ForwardG is 2.0 and its LateralG is 0
- **THEN** its felt G is approximately 2.24

### Requirement: Matching the preferred G makes riders happier

While the ride runs its physics, each seated rider's happiness SHALL increase at `HappinessGainPerSecond × (1 − d / FunBand)` per second when `d < FunBand`, where `d = |feltG − preferredG|`, `HappinessGainPerSecond` = 2 and `FunBand` = 1.0 g. When `d ≥ FunBand` happiness SHALL not change from this rule.

#### Scenario: Perfect match gives the full gain
- **WHEN** a rider prefers 3.0 g and their gondola holds a felt G of 3.0 for 5 seconds
- **THEN** their happiness has increased by 10 points

#### Scenario: Far from the preference gives no gain
- **WHEN** a rider prefers 4.0 g and their gondola holds a felt G of 1.5
- **THEN** their happiness does not increase

### Requirement: Riding too intensely makes riders nauseous

While the ride runs its physics, when a rider's gondola felt G exceeds their preferred G by more than `0.3 × MaxGForce` (1.35 g), the rider's nausea SHALL grow at `NauseaGrowthRate × (nausea + NauseaSeed)` per second, with `NauseaGrowthRate` = 0.1 /s and `NauseaSeed` = 5. Otherwise nausea SHALL not change from this rule.

#### Scenario: Overshoot grows nausea exponentially
- **WHEN** a rider with nausea 0 prefers 2.25 g and their gondola holds a felt G of 4.0 for 10 seconds
- **THEN** their nausea is approximately 8.6 (5 × (e − 1))

#### Scenario: Within tolerance causes no nausea
- **WHEN** a rider prefers 2.5 g and their gondola holds a felt G of 3.5
- **THEN** their nausea does not change

### Requirement: Sustained max G adds a nausea penalty

When a gondola's felt G stays at or above `MaxGForce` for more than 1 second continuously, every rider in that gondola SHALL gain +25 nausea once. The gondola SHALL not apply the penalty again until its felt G has dropped below `MaxGForce` and a new continuous stretch exceeds 1 second.

#### Scenario: One long stretch gives one penalty
- **WHEN** a gondola stays at or above the safe limit for 3 continuous seconds
- **THEN** its riders gain exactly +25 nausea

#### Scenario: Two stretches give two penalties
- **WHEN** a gondola exceeds the safe limit for 1.5 s, drops below it, then exceeds it for 1.5 s again
- **THEN** its riders gain +50 nausea in total

#### Scenario: A short spike gives no penalty
- **WHEN** a gondola is at or above the safe limit for 0.5 s
- **THEN** its riders gain no penalty nausea

### Requirement: Riders keep their identity from queue to exit

A boarded rider SHALL carry their guest number, happiness, preferred G and nausea from the queue into their seat. When the ride offloads, the system SHALL record the offloaded riders with their final mood as the ride's last offload, numbered by an offload counter that increases by one per offload.

#### Scenario: Boarded riders keep their guest number
- **WHEN** a guest with number 42 boards
- **THEN** the seat they occupy reports guest number 42 with the same preferred G they had in the queue

#### Scenario: Offload is recorded
- **WHEN** the ride offloads 6 riders
- **THEN** the last offload lists those 6 riders with their final happiness and nausea, and its counter is one higher than before
