## ADDED Requirements

### Requirement: Dequeue boards the next group

The boarding operation SHALL take the group at the front of the queue, remove its members from the queue, and board them into available gondolas. Each dequeue-and-board operation SHALL process exactly one group.

#### Scenario: The front group is boarded and removed

- **WHEN** boarding is invoked and group X is at the front of the queue
- **THEN** group X's members are assigned to gondolas and group X is no longer in the queue

#### Scenario: Boarding an empty queue boards no one

- **WHEN** boarding is invoked while the queue is empty
- **THEN** no guests are boarded and the operation reports that no group was available

### Requirement: Guests board gondolas in pairs

Members of a boarded group SHALL be seated two per gondola wherever possible, since a gondola seats two. A group SHALL be spread across as many gondolas as needed to seat all its members.

#### Scenario: A pair boards one gondola

- **WHEN** a group of 2 is boarded and at least one gondola is available
- **THEN** both guests are seated in the same gondola

#### Scenario: A large group spans multiple gondolas

- **WHEN** a group of 4 is boarded and at least two gondolas are available
- **THEN** the group is seated as two pairs across two gondolas

### Requirement: An odd group member boards alone

When a boarded group has an odd number of members, the unpaired member SHALL be seated alone in a gondola rather than paired with a guest from a different group, resulting in a single-occupant gondola.

#### Scenario: Odd group produces a single-occupant gondola

- **WHEN** a group of 3 is boarded and at least two gondolas are available
- **THEN** one gondola is seated with 2 members and a second gondola is seated with the remaining 1 member, who rides alone

#### Scenario: Members of different groups are not paired

- **WHEN** a group of 1 is boarded and the next group in the queue also has members waiting
- **THEN** the lone member is seated alone and no member of the following group is placed in that gondola

### Requirement: Boarding is bounded by available gondola capacity

Boarding SHALL only seat as many members as there is available gondola capacity. If a group is larger than the available capacity, the members who could not be seated SHALL remain at the front of the queue as an intact group for a later boarding.

#### Scenario: Group larger than available capacity

- **WHEN** a group of 6 is boarded but only two gondolas (capacity 4) are available
- **THEN** 4 members are seated across the two gondolas and the remaining 2 members stay at the front of the queue as the same group

#### Scenario: No gondolas available

- **WHEN** boarding is invoked and no gondola is available
- **THEN** no members are seated and the front group remains unchanged in the queue

### Requirement: Ride may dispatch when the queue empties

When the queue is empty, the ride SHALL be permitted to dispatch with only the gondolas loaded so far, including gondolas that are empty or hold a single occupant, rather than waiting for a full load.

#### Scenario: Partial dispatch on empty queue

- **WHEN** the queue is empty and at least one gondola is occupied
- **THEN** the ride is allowed to dispatch and the occupied gondolas carry their current occupants while remaining gondolas dispatch empty

#### Scenario: No dispatch when nothing is loaded

- **WHEN** the queue is empty and no gondola is occupied
- **THEN** the ride is not signalled to dispatch
