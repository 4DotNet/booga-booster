## ADDED Requirements

### Requirement: Groups are loaded only while the ride is Loading

The loading coordinator SHALL take groups from the ride's queue and seat their members onto the ride only while the ride is in the `Loading` lifecycle state. In any other state it SHALL NOT take groups from the queue nor seat any passengers.

#### Scenario: No loading while idle

- **WHEN** the ride is `Idle` and groups are waiting in its queue
- **THEN** no group is taken from the queue and no passenger is seated

#### Scenario: Loading begins when the ride enters Loading

- **WHEN** the ride is `Loading` and at least one waiting group fits
- **THEN** the coordinator takes fitting groups from the queue and seats their members

#### Scenario: No loading once the ride is safe or running

- **WHEN** the ride has left `Loading` (for example it is now `Safe` or `Started`) and groups are still waiting
- **THEN** no further group is taken from the queue and no passenger is seated

### Requirement: Free capacity is measured in whole empty gondolas

Because a gondola seats two and members of different groups are never paired in the same gondola, the ride's free boarding capacity SHALL be measured as its empty gondolas: an empty gondola offers two free seats to a boarding group, and a gondola already holding any passenger offers no free seats to another group. A group of `N` members needs `ceil(N / 2)` empty gondolas.

#### Scenario: Free seats come only from empty gondolas

- **WHEN** every gondola either is empty or already holds a passenger from an earlier group
- **THEN** the ride's free capacity for a new group is two seats per empty gondola, and the single free seat beside an earlier group's lone rider is not offered to another group

#### Scenario: A group needs whole gondolas

- **WHEN** a group of 3 is considered for boarding
- **THEN** it fits only when at least 2 empty gondolas are available, seating a pair in one and the remaining member alone in the other

### Requirement: A group is boarded only when it fully fits

A waiting group SHALL be boarded only when the ride has enough free capacity to seat every one of its members together — that is, at least `ceil(N / 2)` empty gondolas for a group of `N`. A group that does not fully fit SHALL NOT be partially seated and SHALL remain in the queue unchanged.

#### Scenario: A fitting group is boarded

- **WHEN** a group of 4 is at the front and the ride has at least 2 empty gondolas
- **THEN** the group is boarded, its 4 members are seated as two pairs, and the group is removed from the queue

#### Scenario: An oversized group is not partially boarded

- **WHEN** a group of 6 (needing 3 empty gondolas) is considered but only 2 empty gondolas remain
- **THEN** no member of the group is seated and the whole group remains in the queue

### Requirement: Loading drains every fitting group from the front of the line

While the ride is `Loading`, the coordinator SHALL repeatedly take the group at the front of the queue and board it for as long as the front group fits, removing each boarded group from the queue and preserving the arrival order of the groups behind it.

#### Scenario: Consecutive fitting groups all board

- **WHEN** the ride is `Loading` with ample empty gondolas and three small groups are waiting in order A, B, C, each of which fits
- **THEN** A, B, and C are each boarded and removed, and the queue is left empty

#### Scenario: Boarding advances the front of the line

- **WHEN** the front group A is boarded and removed
- **THEN** the group that was behind A becomes the new front group

### Requirement: A too-large front group is skipped to backfill with a following group

When the front group does not fit but the ride still has free capacity (at least one empty gondola), the coordinator SHALL look ahead to the groups behind it — up to the third group in line — and board the first of those that fits, leaving every group ahead of it in the queue in its original order. After boarding a backfilled group the coordinator SHALL re-evaluate from the front against the reduced capacity.

#### Scenario: The second group backfills past a too-large front group

- **WHEN** the front group needs more empty gondolas than remain, but the second group in line fits
- **THEN** the second group is boarded and removed, and the front (too-large) group stays at the front of the queue

#### Scenario: The third group is reached when the first two do not fit

- **WHEN** neither the front group nor the second group fits the remaining capacity, but the third group in line fits
- **THEN** the third group is boarded and removed, and the first and second groups remain queued in their original order

#### Scenario: Backfilling continues while capacity and fitting groups remain

- **WHEN** a group has just been backfilled and the front group still does not fit, but another group within the first three positions fits the reduced capacity
- **THEN** that group is also boarded

### Requirement: The ride is full when no group in the look-ahead window fits

When neither the front group nor either of the next two groups in line fits the remaining free capacity — including when the ride has no empty gondola (fewer than two free seats) — the coordinator SHALL stop, take no further group, and treat the ride as full for the current load. Groups beyond the third position in line SHALL NOT be considered.

#### Scenario: No fitting group in the first three positions

- **WHEN** the front group and the next two groups all need more empty gondolas than remain
- **THEN** no group is taken, the ride is considered full, and the queue is left unchanged

#### Scenario: No empty gondola means immediately full

- **WHEN** the ride has no empty gondola remaining
- **THEN** the ride is considered full and no group is taken, regardless of the waiting groups

#### Scenario: A fitting fourth group is not reached

- **WHEN** the first three groups in line do not fit but a fourth group further back would fit
- **THEN** the fourth group is not boarded and the ride is considered full

### Requirement: Groups arriving during loading board immediately when they fit

While the ride is `Loading` and not yet full, a group that joins the queue SHALL be boarded as soon as it fits the ride's remaining free capacity, without the ride having to be reloaded or restarted.

#### Scenario: A newly arrived group boards on the spot

- **WHEN** the ride is `Loading` with free capacity but no currently waiting group fits, and a new group that does fit is then enqueued
- **THEN** the new group is boarded and removed from the queue

#### Scenario: A newly arrived oversized group stays queued

- **WHEN** the ride is `Loading` and full, and a new group is enqueued
- **THEN** the new group remains in the queue and no member is seated while the ride stays full

#### Scenario: A group arriving after loading ends is not boarded

- **WHEN** a group is enqueued after the ride has already left `Loading`
- **THEN** the group is not boarded

### Requirement: Only boarding removes groups, and boarded members become ride passengers

Taking a group for boarding SHALL remove exactly that group from the queue and seat its members onto the ride as passengers who count toward the ride's passenger load; groups that are only inspected or skipped SHALL remain in the queue with their membership intact. Boarding against an empty queue SHALL seat no one and leave the ride unchanged.

#### Scenario: A boarded group leaves the queue and occupies seats

- **WHEN** a group of 2 is boarded
- **THEN** both members occupy seats and count toward the ride's passenger load, and the group is no longer in the queue

#### Scenario: A skipped group is untouched

- **WHEN** a too-large group is skipped so a following group can backfill
- **THEN** the skipped group remains in the queue with all its members, in its original position relative to the groups ahead of it

#### Scenario: Loading an empty queue boards no one

- **WHEN** the ride is `Loading` and its queue is empty
- **THEN** no passenger is seated and the ride is left unchanged
