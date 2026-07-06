## Context

The DigitalTwin owns a rich `Ride` aggregate (`GreatMill` → 4 hubs → 4 gondolas → 2 seats = **16 gondolas / 32 seats**, max combined passenger load **3200 kg**) with a guarded state machine whose `Loading` state is described as "groups are being loaded." Today `Ride.BoardPassenger(hub, gondola, seat, passenger, delay)` seats **one** passenger into an explicit seat and nothing calls it as part of loading. The `add-ride-state-machine` design explicitly parked "Queue dequeue / auto-board" as a follow-up — this change is that follow-up.

The Queue module owns a per-ride `RideQueue` (a `LinkedList<QueuedGroup>` behind a per-ride lock) with `Enqueue`, `PeekNextGroup`, and `SnapshotGroups`, filled by a background `RideQueueFillerService`. It has **no dequeue**, and its public `IRideQueueService` exposes only `EnqueueGroupAsync` + `GetStatus`. Enqueue publishes a `GroupQueuedIntegrationEvent` (`group-queued`) that has **no subscriber**. Queue and DigitalTwin are fully decoupled today and use two separate person/weight types (`Queue.Person.WeightInKilograms:int` vs `DigitalTwin.PassengerWeight:double`).

Constraints from the `4dotnet-csharp-style-guide`: ADR-0003 (rich domain model, intent-revealing methods, `DomainValidationException`), ADR-0004 (cross-module references through `.Abstractions` only), ADR-0005/0006 (CQRS feature slices), ADR-0007 (endpoints in the module), and the xUnit v3 / Moq / Bogus testing guideline (no FluentAssertions).

## Goals / Non-Goals

**Goals:**

- A loading coordinator that, while the ride is `Loading`, pulls fitting groups off the ride's queue and seats their members, implementing free-seat fit, look-ahead/backfill across the first three waiting groups, the "full" stop condition, and immediate boarding of groups that arrive mid-loading.
- A whole-group boarding operation on the `Ride` aggregate so seat allocation and safety invariants stay inside the aggregate.
- A minimal extension to the Queue public contract so the coordinator can inspect the head of the line and remove the specific group it chose to board.
- Deterministic, unit-testable behavior (no wall-clock, no real Dapr) for the fit/look-ahead/full algorithm.

**Non-Goals:**

- The gondola-level pair-seating mechanics themselves (owned by `gondola-boarding`); this change consumes/implements the minimal seating needed and defers refinements there.
- Queue filling and the weather-driven arrival rate (owned by `weather-driven-queue-fill`).
- Auto-entering `Loading` from `Idle` when a group arrives — the operator drives `Idle → Loading` (assumed).
- Weight-based capacity limiting during loading (see Open Questions), persistence, and the frontend.

## Decisions

### 1. The coordinator lives in DigitalTwin and pulls from the queue

The coordinator is a DigitalTwin application service (e.g. `RideLoadingCoordinator`) that references `FourDotnet.BoogaBooster.Queue.Abstractions` (ADR-0004 allows the abstraction reference). DigitalTwin already owns the ride, the seats, the `Loading` state, and boarding authority, so putting the orchestration here keeps seat allocation and safety on the side that owns them; the queue only needs to expose "show me the head groups" and "take this one."

- *Alternative — coordinator in the Controller module:* Controller is the conceptual "operator", but it is an empty scaffold and would need **both** `Queue.Abstractions` and a new boarding surface on `DigitalTwin.Abstractions`, spreading seat/capacity logic across a third module. Rejected as premature.
- *Alternative — coordinator in the Queue module (the `gondola-boarding` `IGondolaAvailabilityProvider` shape):* would force the ride's seat/capacity model out through an abstraction and invert ownership of boarding. Rejected; the ride owns its seats.

### 2. Pull on each Loading tick, not event-driven

The existing `RideSimulationService` already advances the ride ~120 Hz. The coordinator runs one **loading pass** whenever the ride is `Loading`, invoked from the store's advance path under the store's existing lock. A single code path then covers both the initial drain on entering `Loading` and groups that arrive mid-loading (they are simply picked up on the next pass), which is simpler and less racy than reacting to `group-queued`.

- *Alternative — subscribe DigitalTwin to `group-queued` (mirroring `WeatherSubscriptionEndpoints`):* lower latency on arrivals, but needs a separate "loading started" kick for the initial drain, must re-read capacity anyway, and adds a Dapr round-trip to a fundamentally in-process decision. Rejected for now; noted as a latency optimization in Open Questions. The `group-queued` subscriber remains a clean future add because the pass is idempotent.

### 3. Capacity is measured in whole empty gondolas

"Free seats" is derived from empty gondolas: an empty gondola = 2 boardable seats; a gondola already holding any rider offers **0** seats to a different group (no cross-group pairing, per `gondola-boarding`). A group of `N` fits iff `ceil(N / 2) ≤ emptyGondolas`. This honors the pairing rule, avoids "stranded odd seat" bugs (a group of 3 consumes 2 gondolas and wastes one seat by design), and matches the user's own "two or more seats left" threshold, which is exactly "at least one empty gondola."

- *Alternative — raw free-seat count (`32 − occupiedSeats`), fit = `N ≤ freeSeats`:* simpler arithmetic but wrong — it would count the stranded seat next to a lone rider and let a stranger pair with them, violating the no-mixing rule and over-reporting capacity. Rejected.

### 4. Look-ahead window is the first three groups, evaluated iteratively

A loading pass loops: compute free gondolas; scan positions 1–3 for the first group that fits; if found, take + board it and loop again with reduced capacity; if none of the first three fit, stop and treat the ride as full. This is faithful to the description ("next … second … third … else the ride is full") and bounds work per pass. Boarding a fitting front group is just the degenerate case (position 1 fits).

- *Alternative — unbounded best-fit / scan the whole queue:* maximizes utilization but the user explicitly bounded the look-ahead to three and it makes starvation of large groups less predictable. Rejected; keep the described bound.

### 5. Whole-group boarding is a new operation on the `Ride` aggregate

Add `Ride.BoardGroup(...)` taking the group's member weights (and the natural restraint-close delay source) that: verifies `ceil(N/2) ≤ emptyGondolas`, allocates that many empty gondolas, seats members two-per-gondola with the odd member alone, forces state to `Loading`, and marks changed — returning whether it boarded. Expose it through `IRideStore.BoardGroup(...)` (returning fresh `RideTelemetry`) so it runs under the store lock like every other mutation. The coordinator asks the store for free-gondola count and calls `BoardGroup`; it never reaches into individual seats.

- *Alternative — coordinator computes seat coordinates and calls `BoardPassenger` per seat:* leaks seat-allocation and the pairing invariant out of the aggregate and races the simulation. Rejected per ADR-0003.

### 6. Queue contract gains head-inspection + take-by-id

Extend `IRideQueueService` (Queue.Abstractions) with: reuse `GetStatus(rideId)` (already returns ordered `Groups` with `GroupId` + `Size`) for the look-ahead peek, and add `Task<QueuedGroupDto?> TakeGroupAsync(Guid rideId, Guid groupId, CancellationToken)` that removes and returns that specific group, or `null` if it is no longer present. `RideQueue` gains `QueuedGroup? Remove(Guid groupId)` under its existing lock. Taking **by group id** (not by index) is race-safe against the background filler and against a group already taken.

- *Alternative — index/position-based take:* races with concurrent enqueue reordering assumptions. Rejected. The coordinator re-checks fit at take time and treats a `null` (already-gone) result as "skip and re-scan."

### 7. Weight mapping at the boundary

The coordinator maps each `PersonDto.WeightInKilograms` (int) to a `PassengerWeight` (double) when building the passengers to board, so realistic per-rider weights feed the ride's existing `Overloaded`/balance safety checks. `Passenger`/`Person` stay independent types either side of the abstraction.

## Risks / Trade-offs

- **Seat-based fit ignores the 3200 kg weight limit** → a seat-full but heavy load could exceed `MaxPassengerLoadKg`, leaving the ride unable to reach `Safe`. Mitigation: the existing safety guard already blocks `Loading → Safe` while `Overloaded`; the ride is not *broken*, only un-startable until offloaded. Whether loading should also stop on weight is an Open Question, kept out of scope to match the seat-based description.
- **Stale peek vs. concurrent filler** → the look-ahead reads a `GetStatus` snapshot, then takes by id; the group may have changed by take time. Mitigation: `TakeGroupAsync` returns `null` when gone and the coordinator re-scans; fit is re-verified against live free-gondola count inside `BoardGroup`.
- **Large groups can be perpetually backfilled past** → while capacity remains, small groups behind a too-large front group keep boarding, delaying the big group to a future emptier ride. Accepted: max group size is 5 (`⌈5/2⌉ = 3 ≤ 16` gondolas), so a large group always fits a fresh/empty ride; and once the ride is full loading stops for everyone.
- **Per-tick queue reads at 120 Hz** → cheap (in-memory `GetStatus` snapshot, short-circuited when the queue is empty or the ride is not `Loading`), but the pass must be a no-op fast path when there is nothing to do. Mitigation: the pass exits immediately unless `state == Loading` and the queue is non-empty and at least one gondola is empty.
- **Cross-module coupling grows** → DigitalTwin now depends on `Queue.Abstractions`. Mitigation: keep the added surface to `GetStatus` + `TakeGroupAsync` only.

## Migration Plan

Additive; no data migration (ephemeral in-memory ride and queue). Order:

1. **Queue**: add `RideQueue.Remove(groupId)`; add `TakeGroupAsync` to `IRideQueueService` + `RideQueueService`; unit-test dequeue/ordering.
2. **DigitalTwin domain**: add `Ride.BoardGroup(...)` + a free-gondola count; unit-test seating, odd-member-alone, capacity guard.
3. **DigitalTwin application**: add `RideLoadingCoordinator`, expose `IRideStore.BoardGroup`/free-capacity, and invoke a loading pass from the advance path while `Loading`; reference `Queue.Abstractions`; register in `DigitalTwinModuleExtensions`.
4. Tests for the fit / look-ahead / full / mid-loading-arrival algorithm (Moq the queue contract, Bogus for groups). `dotnet build` + `dotnet test` green.

Rollback is removing the coordinator invocation from the advance path; the new Queue/Ride methods are harmless if unused.

## Open Questions

- Should loading also stop before exceeding the **3200 kg** weight limit (weight-aware fit), or is seat-based fit + the existing `Overloaded` start-guard sufficient? Assumed seat-based only for this change.
- Do we need the low-latency `group-queued` subscriber, or is per-tick pull fast enough? Assumed per-tick pull is sufficient (120 Hz).
- Should the ride **auto-enter `Loading`** from `Idle` when groups are waiting, or does the operator always trigger it? Assumed operator-triggered.
- Is the look-ahead depth of **three** fixed, or should it be configurable? Assumed fixed at three per the description.
