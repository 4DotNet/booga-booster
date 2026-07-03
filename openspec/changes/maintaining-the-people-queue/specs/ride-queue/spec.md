## ADDED Requirements

### Requirement: Per-ride queue holds guests in arrival order

Each ride SHALL have its own queue that holds waiting guests in the order they arrived. Enqueuing a guest or group SHALL append it to the back of that ride's queue, and the front of the queue SHALL always represent the guests who have waited longest.

#### Scenario: Guests are ordered by arrival

- **WHEN** guest A is enqueued, then guest B, then guest C onto an empty ride queue
- **THEN** the queue reports A at the front, followed by B, then C

#### Scenario: Queues are isolated per ride

- **WHEN** a guest is enqueued onto ride 1's queue
- **THEN** ride 2's queue length is unchanged and does not contain that guest

### Requirement: Group membership is preserved

The queue SHALL preserve the relationship between guests who arrive together as a group. Members of the same group SHALL remain contiguous in the queue and SHALL be identifiable as belonging to the same group, so they can later be boarded together.

#### Scenario: A group is enqueued as a contiguous unit

- **WHEN** a group of 3 guests is enqueued
- **THEN** all 3 guests occupy adjacent positions in the queue and share the same group identifier

#### Scenario: A later group does not split an earlier group

- **WHEN** group X (2 guests) is enqueued and then group Y (2 guests) is enqueued
- **THEN** the queue order is the two members of X followed by the two members of Y, with neither group interleaved

#### Scenario: A lone guest is a group of one

- **WHEN** a single guest who arrived alone is enqueued
- **THEN** the guest is represented as a group of size 1 with a unique group identifier

### Requirement: Queue exposes the next group to board

The queue SHALL expose the group at its front as the next group to board, without removing it, so a boarding decision can be made against the current state.

#### Scenario: Peek returns the front group

- **WHEN** group X is at the front of the queue
- **THEN** inspecting the next group returns group X and its members, and the queue length is unchanged

#### Scenario: Peek on an empty queue

- **WHEN** the queue is empty
- **THEN** inspecting the next group returns no group

### Requirement: Queue tracks its lifecycle state

The ride queue SHALL be a domain model that tracks its lifecycle state per the domain-model standard. A queue newly created in memory SHALL start as `New`; a queue rehydrated from the data store SHALL start as `Pristine`; an enqueue or dequeue that changes its contents SHALL move it to `Modified`; an operation that results in no actual change SHALL move it to `Touched`.

#### Scenario: Enqueue marks the queue Modified

- **WHEN** a guest is enqueued onto a `Pristine` queue
- **THEN** the queue's state is `Modified`

#### Scenario: A no-op operation marks the queue Touched

- **WHEN** an operation is applied to a `Pristine` queue that does not change its contents
- **THEN** the queue's state is `Touched`
