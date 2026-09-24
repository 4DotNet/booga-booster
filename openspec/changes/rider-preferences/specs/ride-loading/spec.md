## MODIFIED Requirements

### Requirement: Only boarding removes groups, and boarded members become ride passengers

Taking a group for boarding SHALL remove exactly that group from the queue and seat its members onto the ride as passengers who count toward the ride's passenger load; groups that are only inspected or skipped SHALL remain in the queue with their membership intact. Each seated passenger SHALL carry the member's weight and rider profile — preferred intensity, current (wait-adjusted) happiness and nausea — exactly as the queue returned them when the group was taken. Boarding against an empty queue SHALL seat no one and leave the ride unchanged.

#### Scenario: A boarded group leaves the queue and occupies seats

- **WHEN** a group of 2 is boarded
- **THEN** both members occupy seats and count toward the ride's passenger load, and the group is no longer in the queue

#### Scenario: A boarded member's profile travels with them

- **WHEN** a member with preferred intensity `0.3`, current happiness `0.55` and nausea `0` is boarded
- **THEN** the passenger in their seat reports preferred intensity `0.3`, happiness `0.55` and nausea `0`

#### Scenario: A skipped group is untouched

- **WHEN** a too-large group is skipped so a following group can backfill
- **THEN** the skipped group remains in the queue with all its members, in its original position relative to the groups ahead of it

#### Scenario: Loading an empty queue boards no one

- **WHEN** the ride is `Loading` and its queue is empty
- **THEN** no passenger is seated and the ride is left unchanged
