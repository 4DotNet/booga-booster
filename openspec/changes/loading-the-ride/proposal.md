## Why

The ride now has a `Loading` lifecycle state and a per-ride queue that fills with arriving groups, but nothing connects them: no system pulls groups off the queue and seats them on the ride. The state-machine change deliberately left "Queue dequeue / auto-board" as a follow-up, and the queue can only be peeked — it cannot be drained. Without a loading coordinator the ride can never actually take on passengers, so it can never become safe to start.

## What Changes

- Introduce a **loading coordinator** that, while the ride is in `Loading`, repeatedly pulls the next fitting group off the ride's queue and boards its members onto the ride, until no waiting group fits the remaining capacity.
- Board a group by **free-seat fit**: a group is taken only when the ride has enough boarding capacity to seat all of its members; a group that is larger than the remaining capacity is never partially split.
- Add **look-ahead / backfill**: when the group at the front is too large but capacity remains, the coordinator considers the next groups in line (up to the third group) and boards the first one that fits, leaving the too-large group at the front for a later, emptier ride. When none of the first three waiting groups fit, loading stops and the ride is considered **full**.
- Board **newly-arrived groups immediately**: a group that joins the queue while the ride is still loading is boarded on the spot if it fits the remaining capacity.
- **BREAKING (internal contract)**: extend the Queue module's public contract so a boarding caller can inspect the head of the line and **remove a specific chosen group** (not only the front one) when it is boarded — the queue is currently peek-only.

## Capabilities

### New Capabilities
- `ride-loading`: The loading coordinator that drains the ride's queue into free seats while the ride is `Loading` — free-seat fit, look-ahead/backfill across the first three waiting groups, the "full" stopping condition, and immediate boarding of groups that arrive mid-loading.

### Modified Capabilities
<!-- None — openspec/specs/ has no published baseline capabilities yet. The queue-side
     dequeue/selective-take this change relies on is delivered here as part of ride-loading. -->

## Impact

- **DigitalTwin module** (`src/DigitalTwin/FourDotnet.BoogaBooster.DigitalTwin`): a new loading coordinator (application service driven by the existing `Loading` state), a group-boarding operation on the `Ride` aggregate that seats a whole group across gondolas, and a way to report the ride's current free-seat / boarding capacity.
- **Queue module** (`src/Queue/FourDotnet.BoogaBooster.Queue` + `.Abstractions`): `RideQueue` gains a dequeue/selective-take operation; `IRideQueueService` (Queue.Abstractions) exposes the head-of-line groups and a "take this group" operation so the coordinator can pull the group it chose.
- **Cross-module dependency**: DigitalTwin references `FourDotnet.BoogaBooster.Queue.Abstractions` only (ADR-0004). No module-to-module code coupling beyond the abstraction.
- **Depends on**: the `Loading` state from `add-ride-state-machine` and the per-ride queue from `maintaining-the-people-queue` (`ride-queue`, `gondola-boarding`). This change turns their `Loading` "boarding window" into real boarding.
- **Testing**: new xUnit (`xunit.v3`) coverage for the fit/look-ahead/full algorithm and mid-loading arrivals, using Moq for the queue contract and Bogus for groups (no FluentAssertions, per the unit-testing guideline).
- **Out of scope**: the gondola-level pair-seating mechanics (owned by `gondola-boarding`), queue filling, the frontend, and persistence.
