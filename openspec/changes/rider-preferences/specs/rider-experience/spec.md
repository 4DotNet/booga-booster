## ADDED Requirements

### Requirement: A passenger carries a rider profile

Every passenger seated on the ride SHALL carry a preferred intensity in `[0.1, 1]`, a happiness in `[0, 1]` and a nausea rating in `[0, 1]`, validated on construction exactly like the passenger's weight. The preferred intensity SHALL be immutable for the duration of the ride; happiness and nausea SHALL change only through the passenger's intent-revealing experience methods and SHALL always stay clamped to `[0, 1]`.

#### Scenario: A passenger is created with a profile

- **WHEN** a passenger is created with weight `80 kg`, preferred intensity `0.4`, happiness `0.7` and nausea `0`
- **THEN** the passenger reports those values

#### Scenario: An invalid profile is rejected

- **WHEN** a passenger is created with preferred intensity `0`
- **THEN** a domain validation error is raised

#### Scenario: A manually boarded passenger gets a sampled profile

- **WHEN** a passenger is boarded through the board-passenger command without a profile
- **THEN** the passenger's profile is drawn from the ride event sampler with a preferred intensity in `[0.1, 1]`, a happiness in `[0.65, 0.85]` and a nausea rating of `0`

### Requirement: Gondola intensity is its felt horizontal G relative to the maximum allowed G

Each gondola SHALL expose an intensity in `[0, 1]` computed every physics tick as the magnitude of its felt horizontal specific force (the vector sum of its lateral and forward G) divided by the maximum allowed G (`4.5 g`), clamped to `1`. A gondola SHALL be considered at the G limit when its felt horizontal G is at or above the maximum allowed G.

#### Scenario: A stationary gondola has zero intensity

- **WHEN** the ride is at rest
- **THEN** every gondola's intensity is `0` and no gondola is at the G limit

#### Scenario: Intensity scales with horizontal G

- **WHEN** a gondola's felt lateral and forward G combine to `2.25 g`
- **THEN** its intensity is `0.5`

#### Scenario: Intensity saturates at the limit

- **WHEN** a gondola's felt horizontal G is `5 g`
- **THEN** its intensity is `1` and the gondola is at the G limit

### Requirement: A matching intensity makes riders happier

While the ride is in motion, a seated passenger whose preferred intensity is within the match tolerance (`0.1`) of their gondola's intensity SHALL gain happiness at `0.1` per second of simulated time, clamped to `1`. Mood SHALL NOT change while the ride is not in motion (loading, safe, offloading, idle).

#### Scenario: A matched rider gains happiness

- **WHEN** a passenger with preferred intensity `0.6` and happiness `0.7` rides for two seconds in a gondola whose intensity stays at `0.6`
- **THEN** their happiness is `0.9`

#### Scenario: Match tolerance is symmetric

- **WHEN** a passenger with preferred intensity `0.6` rides in a gondola whose intensity is `0.5` or `0.7`
- **THEN** their happiness increases

#### Scenario: Happiness is capped

- **WHEN** a matched passenger is already at happiness `1`
- **THEN** their happiness stays `1`

#### Scenario: Mood is frozen while loading

- **WHEN** a passenger sits in a stationary gondola while the ride is loading for ten seconds
- **THEN** their happiness and nausea are unchanged

### Requirement: A ride more intense than preferred makes riders unhappy and nauseous

While the ride is in motion, a seated passenger whose gondola's intensity exceeds their preferred intensity by more than the match tolerance SHALL lose happiness at `0.1` per second and gain nausea at `0.2` per second of simulated time, each clamped to `[0, 1]`. A gondola whose intensity is more than the tolerance below the passenger's preferred intensity SHALL leave their mood unchanged.

#### Scenario: A rider who prefers a calm ride suffers on an intense one

- **WHEN** a passenger with preferred intensity `0.2`, happiness `0.8` and nausea `0` rides for two seconds in a gondola whose intensity stays at `1`
- **THEN** their happiness is `0.6`
- **AND** their nausea is `0.4`

#### Scenario: Just outside the tolerance counts as too intense

- **WHEN** a passenger with preferred intensity `0.6` rides in a gondola whose intensity is `0.75`
- **THEN** their happiness decreases and their nausea increases

#### Scenario: Nausea is capped

- **WHEN** a passenger's nausea would exceed `1`
- **THEN** it is `1`

#### Scenario: A tamer ride leaves mood unchanged

- **WHEN** a passenger with preferred intensity `0.9` rides in a gondola whose intensity is `0.3`
- **THEN** their happiness and nausea are unchanged

### Requirement: Sustained maximum G adds a one-off nausea penalty

A gondola SHALL accumulate the time it spends continuously at the G limit. When that time first reaches two seconds, the gondola SHALL add `0.5` nausea (clamped to `1`) to every passenger seated in it, exactly once. The accumulator SHALL reset, and the penalty SHALL re-arm, only once the gondola's felt horizontal G drops below the maximum allowed G. The penalty SHALL apply regardless of the passengers' preferred intensity.

#### Scenario: Two seconds at the limit adds the penalty

- **WHEN** a gondola carrying a passenger with nausea `0.1` stays at the G limit for two seconds
- **THEN** the passenger's nausea includes an additional `0.5` from the penalty

#### Scenario: The penalty is applied once per episode

- **WHEN** the same gondola stays at the G limit for five seconds
- **THEN** the penalty has been applied exactly once

#### Scenario: A short excursion does not trigger the penalty

- **WHEN** a gondola is at the G limit for one second, drops below it, and is at the limit again for one second
- **THEN** no penalty is applied

#### Scenario: Dropping below the limit re-arms the penalty

- **WHEN** a gondola stays at the G limit for two seconds, drops below it, and then stays at the limit for another two seconds
- **THEN** the penalty has been applied twice

#### Scenario: Thrill seekers are not exempt

- **WHEN** a passenger with preferred intensity `1` rides in a gondola at the G limit for two seconds
- **THEN** their nausea includes the `0.5` penalty

### Requirement: Boarding carries the profile onto the ride

When a group boards from the queue, each member SHALL be seated as a passenger whose weight, preferred intensity, happiness and nausea equal the values the queue returned for that member at the moment the group was taken.

#### Scenario: A boarded member keeps their profile

- **WHEN** a queued person with preferred intensity `0.3`, current happiness `0.55` and nausea `0` is boarded
- **THEN** the seated passenger reports preferred intensity `0.3`, happiness `0.55` and nausea `0`

### Requirement: Telemetry carries a rider-mood roll-up

The ride telemetry snapshot SHALL carry a rider-mood roll-up consisting of the number of seated passengers, their average happiness and their average nausea. Both averages SHALL be absent (null) when nobody is seated. The roll-up SHALL cover passengers on the ride only, never the queue.

#### Scenario: Roll-up averages the seated passengers

- **WHEN** two passengers are seated with happiness `0.6` and `0.8` and nausea `0.2` and `0.4`
- **THEN** the snapshot reports a rider count of `2`, an average happiness of `0.7` and an average nausea of `0.3`

#### Scenario: Roll-up is empty on an empty ride

- **WHEN** nobody is seated
- **THEN** the snapshot reports a rider count of `0` and no average happiness or nausea

#### Scenario: Roll-up follows the riders over time

- **WHEN** the ride runs and the seated passengers' mood changes
- **THEN** successive snapshots reflect the changed averages

### Requirement: Rider-mood observability records aggregates only

The ride telemetry query SHALL tag its span with the average rider happiness and average rider nausea. When passengers leave the ride during offloading, their final happiness and nausea SHALL be recorded in two untagged histograms. No span or metric SHALL identify an individual passenger.

#### Scenario: Telemetry query span carries the averages

- **WHEN** the ride telemetry query completes with passengers aboard
- **THEN** its activity carries `ride.riders.happiness.average` and `ride.riders.nausea.average`

#### Scenario: Offloading records final mood distributions

- **WHEN** three passengers leave during offloading
- **THEN** the final-happiness histogram and the final-nausea histogram each receive three measurements without tags
