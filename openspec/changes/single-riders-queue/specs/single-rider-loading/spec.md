## ADDED Requirements

### Requirement: Group loading runs first and unchanged

While the ride is `Loading`, the loading procedure SHALL first load groups from the regular queue exactly as it does today — free-seat fit on whole empty gondolas, the look-ahead window across the first three waiting groups, and stopping when none of them fits. Introducing single riders SHALL NOT change which groups are chosen, in what order, or when group loading stops.

#### Scenario: Groups are still preferred over individuals

- **WHEN** the ride is `Loading` with capacity for a waiting group and single riders are also waiting
- **THEN** the group is boarded before any single rider is seated

#### Scenario: The look-ahead window is unchanged

- **WHEN** the front group is too large but the second waiting group fits, and single riders are waiting
- **THEN** the second group is boarded by backfill exactly as before, and no single rider is seated in its place

#### Scenario: A group is never split to make room for a single rider

- **WHEN** a waiting group does not fully fit the remaining empty gondolas
- **THEN** no member of that group is seated and the group remains in the regular queue

### Requirement: Single riders top up the ride only after group loading can place no more

Individuals from the single-riders queue SHALL be seated only once the group-loading phase can place no further group — because no waiting group fits the remaining empty gondolas, or because the regular queue is empty. The top-up SHALL run within the same loading pass, immediately after group loading.

#### Scenario: Top-up begins when no group fits

- **WHEN** group loading stops because none of the waiting groups fits the remaining capacity, and free seats remain
- **THEN** single riders are seated into the remaining free seats in the same pass

#### Scenario: Top-up begins when the regular queue is empty

- **WHEN** the regular queue is empty, free seats remain, and single riders are waiting
- **THEN** single riders are seated into the free seats

#### Scenario: No top-up while a group can still be loaded

- **WHEN** a waiting group still fits the remaining empty gondolas
- **THEN** no single rider is seated yet in that iteration

### Requirement: Single riders may be seated next to a stranger

For single riders only, the rule that members of different parties never share a gondola SHALL be relaxed: a single rider MAY be seated in any unoccupied seat, including the free seat beside a passenger from an already-boarded group or beside another single rider. This relaxation SHALL apply only to single riders; a group SHALL still be seated only into entirely empty gondolas.

#### Scenario: A single rider fills the seat beside a group's lone member

- **WHEN** a gondola holds one member of an earlier group and its other seat is free, and a single rider is waiting
- **THEN** the single rider is seated in that free seat

#### Scenario: Two single riders share a gondola

- **WHEN** an empty gondola remains and two single riders are waiting
- **THEN** both are seated in that gondola

#### Scenario: Groups still take only empty gondolas

- **WHEN** a gondola holds a single rider and its other seat is free, and a group of two is waiting
- **THEN** the group is not seated in that gondola and needs an empty gondola instead

### Requirement: The top-up fills every remaining free seat it can

The top-up SHALL seat as many individuals as there are unoccupied seats on the ride, taking them from the front of the single-riders queue in arrival order. Every individual seated SHALL be removed from the single-riders queue.

#### Scenario: Every free seat is filled

- **WHEN** five seats are unoccupied after group loading and at least five single riders are waiting
- **THEN** five single riders are seated, the ride has no unoccupied seat left, and those five have left the single-riders queue

#### Scenario: Fewer riders than seats

- **WHEN** five seats are unoccupied and two single riders are waiting
- **THEN** both are seated, three seats stay unoccupied, and the single-riders queue is empty

#### Scenario: Riders are seated in arrival order

- **WHEN** two seats are unoccupied and single riders A, B and C are waiting in that order
- **THEN** A and B are seated and C remains at the front of the single-riders queue

### Requirement: The top-up stops when the ride is full or nobody is waiting

The top-up SHALL do nothing when the ride has no unoccupied seat, and SHALL do nothing when the single-riders queue is empty. In neither case SHALL it fail or disturb the ride or the queues.

#### Scenario: A full ride takes nobody

- **WHEN** every seat on the ride is occupied and single riders are waiting
- **THEN** no individual is taken from the single-riders queue and nobody is seated

#### Scenario: An empty single-riders line seats nobody

- **WHEN** free seats remain but no single rider is waiting
- **THEN** nobody is seated and the ride departs with those seats unoccupied

### Requirement: Single riders board only while the ride is loading

Individuals SHALL be taken from the single-riders queue and seated only while the ride is in the `Loading` lifecycle state. In any other state no individual SHALL be taken from the single-riders queue and none SHALL be seated.

#### Scenario: No top-up while idle

- **WHEN** the ride is `Idle` and single riders are waiting
- **THEN** no individual is taken from the single-riders queue and nobody is seated

#### Scenario: No top-up once the ride has left loading

- **WHEN** the ride is `Safe` or `Started` and single riders are waiting
- **THEN** no individual is taken from the single-riders queue and nobody is seated

### Requirement: Individuals arriving while the ride loads are picked up

Because a loading pass repeats while the ride is `Loading`, an individual who joins the single-riders queue during loading SHALL be seated on a later pass if free seats still remain and no group can be loaded.

#### Scenario: A late-arriving single rider still boards

- **WHEN** the ride is still `Loading` with a free seat remaining and an individual joins the single-riders queue
- **THEN** that individual is seated on a subsequent loading pass

### Requirement: A single rider who cannot be seated is not lost

If individuals have been taken from the single-riders queue but cannot be seated on the ride, they SHALL be returned to the front of the single-riders queue rather than discarded, so that no guest disappears and none loses their place.

#### Scenario: Un-seatable riders go back to the front

- **WHEN** individuals are taken for the top-up and seating them does not succeed
- **THEN** those individuals are back at the front of the single-riders queue, in their original order, and none of them is seated
