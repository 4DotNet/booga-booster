## ADDED Requirements

### Requirement: Every ride has a single-riders line beside its group queue

Each ride SHALL maintain a single-riders queue in addition to its regular group queue. The single-riders queue SHALL hold individual guests, each waiting on their own, and SHALL be independent of the group queue: enqueuing or taking from one SHALL NOT change the contents or the order of the other.

#### Scenario: The two lines are independent

- **WHEN** a group of four joins a ride's regular queue and two individuals join the same ride's single-riders queue
- **THEN** the regular queue holds one group of four and the single-riders queue holds two individuals, and neither line reports the other's people

#### Scenario: A group of one is not a single rider

- **WHEN** a party of one person joins the regular queue
- **THEN** it is held in the regular queue as a group of one and does not appear in the single-riders queue

#### Scenario: A ride that has never been used has empty lines

- **WHEN** the queue state of a ride that nobody has joined is requested
- **THEN** both the regular queue and the single-riders queue report zero people waiting

### Requirement: Single riders wait in arrival order

The single-riders queue SHALL preserve the order in which individuals joined it. Individuals SHALL be taken from the front of the line, so the guest who has waited longest is served first.

#### Scenario: Arrival order is preserved

- **WHEN** individuals A, B and C join the single-riders queue in that order
- **THEN** the queue reports them in the order A, B, C

#### Scenario: Taking serves the longest-waiting guest first

- **WHEN** individuals A, B and C are waiting and two individuals are taken
- **THEN** A and B are returned, and C remains at the front of the line

### Requirement: The single-riders queue has its own capacity

The single-riders queue SHALL have a configurable maximum number of people. When the line is at that maximum, a further individual SHALL NOT be added and SHALL NOT displace anyone already waiting. This capacity SHALL be independent of the regular queue's capacity.

#### Scenario: A full single-riders line refuses more arrivals

- **WHEN** the single-riders queue is at its configured maximum and another individual arrives
- **THEN** the individual is not added and the people already waiting are unchanged

#### Scenario: A full single-riders line does not block group arrivals

- **WHEN** the single-riders queue is at its maximum while the regular queue still has room
- **THEN** an arriving group still joins the regular queue

### Requirement: Individuals are taken from the single-riders line in bulk

The queue SHALL offer an operation that takes up to a requested number of individuals from the front of the single-riders line and removes them from it. When fewer individuals are waiting than requested, it SHALL return everyone waiting and leave the line empty. When nobody is waiting, it SHALL return no individuals rather than failing. A request for zero or fewer individuals SHALL take nobody.

#### Scenario: Taking fewer than are waiting

- **WHEN** five individuals are waiting and three are requested
- **THEN** the first three are returned and removed, and two remain waiting in their original order

#### Scenario: Taking more than are waiting

- **WHEN** two individuals are waiting and five are requested
- **THEN** both are returned and removed, and the line is left empty

#### Scenario: Taking from an empty line

- **WHEN** nobody is waiting in the single-riders queue and individuals are requested
- **THEN** no individuals are returned and no error is raised

#### Scenario: Taking none

- **WHEN** zero individuals are requested
- **THEN** nobody is taken and the line is unchanged

### Requirement: Individuals taken but not seated return to the front of the line

When individuals have been taken from the single-riders queue but could not be seated, they SHALL be returned to the **front** of the line in their original relative order, so that a failed boarding does not lose a guest or cost them their place.

#### Scenario: Un-seated individuals keep their place

- **WHEN** individuals A and B are taken while C is still waiting, and A and B cannot be seated
- **THEN** A and B are returned to the front, and the line reads A, B, C

### Requirement: The queue state reports both lines

The queue state reported for a ride SHALL include the single-riders line — the number of individuals waiting and the individuals themselves — alongside the existing group information. The reported count of people waiting in groups SHALL continue to count only people in groups, so that single riders are reported as their own figure and never conflated with group members.

#### Scenario: Both lines appear in the queue state

- **WHEN** a ride has three groups totalling nine people and four single riders waiting
- **THEN** the queue state reports three groups, nine people waiting in groups, and four single riders

#### Scenario: Single riders do not inflate the group headcount

- **WHEN** single riders join a ride whose group queue is unchanged
- **THEN** the reported group count and people-waiting-in-groups figures are unchanged

### Requirement: The queue endpoint exposes the single-riders line

The ride queue endpoint SHALL return the single-riders line as part of the queue state it serves, so a client obtains both lines from a single request.

#### Scenario: One request returns both lines

- **WHEN** a client requests a ride's queue state
- **THEN** the response carries the waiting groups and the single-riders line together

### Requirement: The background arrival process fills the single-riders line

The background process that populates ride queues SHALL, on each of its cycles, also add a random number of individuals to each configured ride's single-riders queue, within configurable minimum and maximum bounds. This SHALL be an additional arrival stream: the number of people arriving in groups SHALL NOT be reduced to make room for it.

#### Scenario: Individuals arrive every cycle

- **WHEN** a fill cycle runs for a ride
- **THEN** a number of individuals within the configured bounds is added to that ride's single-riders queue

#### Scenario: Group arrivals are unaffected

- **WHEN** a fill cycle runs with the single-riders stream enabled
- **THEN** the number of people arriving in groups is the same as it would be without the single-riders stream

#### Scenario: Filling stops at the single-riders capacity

- **WHEN** a fill cycle would add more individuals than the single-riders queue can still hold
- **THEN** individuals are added only up to the capacity and the remainder of that cycle's individuals are dropped

### Requirement: Single-rider arrivals follow the weather

The number of individuals arriving in the single-riders queue per cycle SHALL be scaled by the current weather in the same way group arrivals are, so that poor weather suppresses lone guests exactly as it suppresses parties.

#### Scenario: Poor weather suppresses lone guests

- **WHEN** a fill cycle runs while the weather indicator is at its worst
- **THEN** no individual is added to the single-riders queue that cycle

#### Scenario: Good weather admits the planned number

- **WHEN** a fill cycle runs while the weather indicator is at its best
- **THEN** the planned number of individuals within the configured bounds is added

### Requirement: An individual joining the single-riders line is announced

Adding an individual to a ride's single-riders queue SHALL publish an integration event naming the ride, the person and the moment they joined, so other parts of the system can react to lone arrivals as they already can to group arrivals.

#### Scenario: Joining publishes an event

- **WHEN** an individual is added to a ride's single-riders queue
- **THEN** an event is published identifying the ride, that person and the time they joined

### Requirement: The frontend shows the single-riders line

The queue panel SHALL display the number of single riders waiting alongside the groups queued and the people waiting, drawn from the same polled queue state, and its worded summary SHALL include the single riders so it is announced to assistive technology like the existing figures.

#### Scenario: The panel shows single riders

- **WHEN** the queue state reports four single riders waiting
- **THEN** the panel displays the single-riders figure beside the groups-queued and people-waiting figures

#### Scenario: The summary announces single riders

- **WHEN** the queue state is available
- **THEN** the worded summary states how many single riders are waiting, correctly singular or plural

#### Scenario: An unavailable feed shows no single-rider figure

- **WHEN** the queue state cannot be loaded
- **THEN** the panel shows its unavailable message and no single-rider figure
