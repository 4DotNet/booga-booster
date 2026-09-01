# gondola-boarding Specification

## Purpose

Defines the dequeue-and-board operation: taking the next fitting group from the queue and seating its members across empty gondolas in pairs, permitting a single-occupant gondola for an odd member, splitting oversized groups on arrival, and allowing partial dispatch when the queue empties.

## Requirements

### Requirement: Dequeue boards the next group that fits

The boarding operation SHALL take a waiting group, remove its members from the queue, and board them into empty gondolas. Each dequeue-and-board operation SHALL process exactly one group. The group taken SHALL be the group nearest the front of the queue whose members all fit the remaining capacity, so a too-large group at the front does not stall the guests behind it.

#### Scenario: The chosen group is boarded and removed

- **WHEN** boarding is invoked and group X is the group nearest the front that fits
- **THEN** group X's members are assigned to gondolas and group X is no longer in the queue

#### Scenario: Boarding an empty queue boards no one

- **WHEN** boarding is invoked while the queue is empty
- **THEN** no guests are boarded and the operation reports that no group was available

### Requirement: Guests board gondolas in pairs

Members of a boarded group SHALL be seated two per gondola wherever possible, since a gondola seats two. A group SHALL be spread across as many gondolas as needed to seat all its members, and SHALL NOT share a gondola with another group.

#### Scenario: A pair boards one gondola

- **WHEN** a group of 2 is boarded and at least one gondola is empty
- **THEN** both guests are seated in the same gondola

#### Scenario: A large group spans multiple gondolas

- **WHEN** a group of 4 is boarded and at least two gondolas are empty
- **THEN** the group is seated as two pairs across two gondolas

### Requirement: An odd group member boards alone

When a boarded group has an odd number of members, the unpaired member SHALL be seated alone in a gondola rather than paired with a guest from a different group, resulting in a single-occupant gondola.

#### Scenario: Odd group produces a single-occupant gondola

- **WHEN** a group of 3 is boarded and at least two gondolas are empty
- **THEN** one gondola is seated with 2 members and a second gondola is seated with the remaining 1 member, who rides alone

#### Scenario: Members of different groups are not paired

- **WHEN** a group of 1 is boarded and the next group in the queue also has members waiting
- **THEN** the lone member is seated alone and no member of the following group is placed in that gondola

### Requirement: A group boards only when it fully fits

A group SHALL be boarded only when every one of its members can be seated in the boarding pass, requiring `ceil(N / 2)` empty gondolas for a group of `N`. A group SHALL NOT be boarded partially and SHALL NOT be split across two boarding passes or two dispatches: when it does not fully fit, it stays in the queue intact and waits for a pass with enough room.

#### Scenario: A group that does not fit stays intact in the queue

- **WHEN** a group of 6 would be boarded but only two gondolas (4 seats) are empty
- **THEN** none of its members are seated and the group remains in the queue unchanged as the same group

#### Scenario: No gondolas empty

- **WHEN** boarding is invoked and no gondola is empty
- **THEN** no members are seated and the queue is unchanged

#### Scenario: A waiting group boards once the room exists

- **WHEN** a group that did not fit an earlier pass is offered a pass with enough empty gondolas
- **THEN** all of its members are seated together in that pass

### Requirement: A group too large for the ride is split on arrival

A group SHALL NOT be allowed to wait in a queue if it can never fit an empty ride, because such a group would never satisfy the fully-fits rule and would wait forever. When an arriving group is larger than the ride's total seat capacity — 32 people, being 16 gondolas of 2 seats — it SHALL be split on arrival into the smallest number of boardable groups, each no larger than that capacity, sized as evenly as possible and enqueued adjacently in arrival order. The capacity at which splitting occurs SHALL be configurable. Groups at or below the capacity SHALL never be split.

#### Scenario: A group within capacity is not split

- **WHEN** a group of 32 arrives at a ride
- **THEN** it is enqueued as a single group of 32

#### Scenario: An oversized group is split into boardable groups

- **WHEN** a group of 40 arrives at a ride
- **THEN** it is enqueued as two adjacent groups of 20, each with its own group identity
- **AND** each resulting group is small enough to be boarded in a single pass

#### Scenario: Splitting divides the group as evenly as possible

- **WHEN** a group of 33 arrives at a ride
- **THEN** it is enqueued as two adjacent groups of 17 and 16 rather than 32 and 1

### Requirement: Ride may dispatch when the queue empties

When the queue is empty, the ride SHALL be permitted to dispatch with only the gondolas loaded so far, including gondolas that are empty or hold a single occupant, rather than waiting for a full load.

#### Scenario: Partial dispatch on empty queue

- **WHEN** the queue is empty and at least one gondola is occupied
- **THEN** the ride is allowed to dispatch and the occupied gondolas carry their current occupants while remaining gondolas dispatch empty

#### Scenario: No dispatch when nothing is loaded

- **WHEN** the queue is empty and no gondola is occupied
- **THEN** the ride is not signalled to dispatch
