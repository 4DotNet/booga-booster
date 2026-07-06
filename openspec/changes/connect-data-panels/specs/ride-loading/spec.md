## ADDED Requirements

### Requirement: Boarding fills randomly chosen empty gondolas

When a group is boarded, the coordinator SHALL seat its members into empty gondolas chosen at random from all currently empty gondolas, rather than always the first empty gondolas in a fixed hub-by-hub order. Random selection SHALL NOT relax the existing boarding rules: a group of `N` still occupies exactly `ceil(N / 2)` empty gondolas, seated two per gondola, and never shares a gondola with another group. Only empty gondolas SHALL be eligible for selection.

#### Scenario: A pair lands in a randomly selected empty gondola

- **WHEN** a group of 2 is boarded while several gondolas are empty
- **THEN** the pair occupies one empty gondola selected at random from the empty gondolas
- **AND** the chosen gondola is not required to be the first empty gondola in fixed order

#### Scenario: Load distributes across the mill over repeated boardings

- **WHEN** many small groups board over successive loading passes with ample empty gondolas
- **THEN** the occupied gondolas are spread across the mill rather than always filling the same fixed sequence first

#### Scenario: Random selection still respects capacity

- **WHEN** a group of 4 is boarded and exactly 2 empty gondolas remain
- **THEN** both empty gondolas are used, seating the group as two pairs
- **AND** no gondola already holding another group's passenger is chosen
