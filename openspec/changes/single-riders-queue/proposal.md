## Why

The ride loads by whole groups only: a group boards just when `ceil(N/2)` **empty** gondolas are free, and a gondola that already holds a rider offers nobody else a seat. Every odd-sized group therefore strands a seat, and once no waiting group fits the remaining empty gondolas the ride departs with seats to spare. Real parks solve exactly this with a **single-riders line** — guests willing to ride alone who are used to plug the gaps a group-consistent load leaves behind.

## What Changes

- Add a **single-riders queue** per ride, alongside the existing group queue: an ordered line of individual guests, each waiting on their own, held in the Queue module.
- **Extend the Queue module's public contract** so callers can read the single-riders line and take a specific number of individuals from its front. The queue status returned by `GET /rides/{rideId}/queue` gains the singles line (count plus the waiting people).
- **BREAKING (internal contract)**: `QueueStatusDto` grows single-rider fields, so every consumer of the queue status — the loading coordinator and the Angular queue panel — sees a changed shape.
- Extend the **background filler** to feed the single-riders queue as an additional arrival stream each cycle: a configurable number of lone guests per cycle, subject to the same weather scaling and its own capacity cap, published as its own integration event.
- Extend **ride loading** with a second phase. The current group-loading procedure runs unchanged and first — groups stay together, the look-ahead/backfill window is untouched. Only once no waiting group fits any more does the coordinator **top up every remaining free seat with individuals** from the single-riders queue.
- **BREAKING (domain rule)**: single riders may be seated **next to a stranger**. The "members of different groups never share a gondola" rule is relaxed for single riders only, so the free seat beside a group's lone rider becomes boardable. Whole groups keep the old rule: they still take only entirely empty gondolas.
- Surface the single-riders line in the **frontend queue panel**: singles waiting shown alongside groups queued and people waiting, in the same polled read-only view and its live-region summary.

## Capabilities

### New Capabilities

- `single-riders-queue`: The per-ride single-riders line — enqueuing individual guests, its arrival order and capacity, reading its state through the queue contract and the queue endpoint, taking individuals from its front, and the background arrival stream that keeps it populated.
- `single-rider-loading`: The seat top-up phase of ride loading — that it runs only after group loading can place no further group, that it fills every remaining free seat (including the seat beside an existing rider) with individuals in arrival order, and when it stops.

### Modified Capabilities

<!-- None. openspec/specs/ has no published baseline capabilities yet, so the
     changes to the group-loading procedure and the queue status contract are
     described as part of the two new capabilities above rather than as deltas. -->

## Impact

- **Queue module** (`src/Queue/FourDotnet.BoogaBooster.Queue`): a new `SingleRiderQueue` aggregate holding individual `Person`s in arrival order; the in-memory store gains a per-ride single-riders line; `RideQueueService` gains enqueue/take/report for singles; `RideQueueFillerService` and `ArrivalPlanner` gain the singles arrival stream; `QueueModuleOptions` gains the singles arrival bounds and queue cap.
- **Queue abstractions** (`FourDotnet.BoogaBooster.Queue.Abstractions`): `IRideQueueService` gains `EnqueueSingleRiderAsync` and `TakeSingleRidersAsync`; the queue status DTO gains the singles line. The module's DTOs are relocated into the mandated `DataTransferObjects/<Feature>/` layout while this contract is being reshaped (see Impact note below).
- **DigitalTwin module** (`src/DigitalTwin/FourDotnet.BoogaBooster.DigitalTwin`): `GreatMill`/`Ride` gain a free-seat count and a "seat these individuals into any free seats" operation that may pair strangers; `IRideStore` exposes it; `RideLoadingCoordinator` gains the top-up phase after group loading.
- **Integration messages** (`src/Shared/FourDotnet.BoogaBooster.IntegrationMessages`): a new `single-rider-queued` event mirroring `group-queued`.
- **HTTP API**: `GET /rides/{rideId}/queue` response shape grows (additive fields, but a changed contract for existing clients).
- **Angular app** (`src/FourDotnet.BoogaBooster.App/src/app/queue`): the queue model, HTTP source, state service, panel and their specs/fakes carry the singles line.
- **Testing**: new xUnit (`xunit.v3`) coverage for the singles aggregate, the singles arrival stream, the free-seat seating rule and the two-phase loading pass (Moq for contracts, Bogus for people, no FluentAssertions); Vitest coverage for the frontend changes.
- **Style-guide discrepancy to fix**: the Queue module's existing DTOs (`PersonDto`, `QueuedGroupDto`, `QueueStatusDto`) sit at the Abstractions project root, which violates the mandated `Abstractions/DataTransferObjects/<Feature>/` layout. Since this change reshapes that contract anyway, the relocation is in scope for the Queue module only.
- **Out of scope**: single riders jumping an otherwise-full ride's group queue, letting a single rider split an arriving group, the ride's 3200 kg weight limit as a loading constraint, persistence, and any change to the group look-ahead window.
