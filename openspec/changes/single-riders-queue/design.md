## Context

The Queue module owns one aggregate per ride, `RideQueue` — a `LinkedList<QueuedGroup>` behind a per-ride `Lock`, held in a singleton `InMemoryRideQueueStore` and filled by the hosted `RideQueueFillerService`. Its contract, `IRideQueueService`, offers `EnqueueGroupAsync`, `GetStatus` and `TakeGroupAsync(rideId, groupId)`; enqueuing publishes `GroupQueuedIntegrationEvent` (`group-queued`).

The DigitalTwin's `RideLoadingCoordinator` runs a loading pass every simulation tick while the ride is `Loading`: it reads `GetStatus`, scans the first three waiting groups for one whose `ceil(N/2)` fits `IRideStore.EmptyGondolaCount`, takes it by id, and calls `IRideStore.BoardGroup(members)`. `Ride.BoardGroup` requires whole empty gondolas and seats members two per gondola with an odd member alone — `GreatMill.EmptyGondolaCount` deliberately counts only *entirely* empty gondolas, so a gondola holding a lone rider contributes zero capacity. That is precisely the seat this change reclaims.

The Angular queue panel polls `GET /rides/{rideId}/queue` every 5 s through `HttpQueueSource` → `QueueStateService` → `QueuePanel`, projecting `groupCount` and `peopleWaiting` into a read-only panel with a polite live region.

Constraints from the `4dotnet-csharp-style-guide`: ADR-0001 (net10.0), ADR-0002 (minimal APIs), ADR-0003 (rich domain model, intent-revealing methods, `DomainValidationException`), ADR-0004 (cross-module references through `.Abstractions` only), ADR-0006 (feature-slice organization), ADR-0007 (endpoints in the module), ADR-0009 (OpenTelemetry), and the unit-testing guideline (xUnit v3, Moq, Bogus, at least 80% module coverage, no FluentAssertions). The repo's DTO-organization rule mandates `Abstractions/DataTransferObjects/<Feature>/<Feature>Request|Response`; the Queue module currently violates it.

## Goals / Non-Goals

**Goals:**

- A per-ride single-riders line in the Queue module: its own aggregate, its own capacity, its own arrival stream from the background filler, its own integration event.
- A minimal, race-safe extension of the Queue contract so a boarding caller can read the singles line and take the first `n` individuals from it.
- A second loading phase in the DigitalTwin that tops up **every** remaining free seat — including the seat beside an existing rider — with individuals, only after the existing group procedure has placed everything it can.
- The singles line visible in the frontend queue panel through the same polled read-only path.
- Deterministic unit tests for the new aggregate, the arrival stream, the free-seat seating rule and the two-phase pass.

**Non-Goals:**

- Changing the group-loading procedure: the look-ahead window of three, the `ceil(N/2)` whole-empty-gondola fit and the group's no-mixing rule stay exactly as they are.
- Weight-aware loading (the 3200 kg limit) as a stopping condition — still governed by the existing `Overloaded` start guard.
- Splitting an arriving group to fill seats, or promoting a waiting group of one into the singles line.
- Persistence, and any change to the Weather or Controller modules.

## Decisions

### 1. A separate `SingleRiderQueue` aggregate, not a lane inside `RideQueue`

A new aggregate root `SingleRiderQueue` (`Domain/SingleRiderQueue.cs`) holds `LinkedList<Person>` in arrival order behind its own `Lock`, with `RideId`, `MaxPeople`, `PeopleWaiting`, `CanAccept(int)`, `Enqueue(Person)`, `TakeUpTo(int count)` (removes and returns up to `count` from the front, fewer when the line is shorter) and `SnapshotPeople()`.  `InMemoryRideQueueStore` gains a second `ConcurrentDictionary<Guid, SingleRiderQueue>` with `GetOrCreateSingles(rideId)` / `FindSingles(rideId)`.

Keeping the two lines apart means a *group of one* — a party that happens to be one person and still boards as a group under the old rules — is never confused with a *single rider*, a guest who consented to being paired with a stranger. Those are different consents, so they are different types.

- *Alternative — reuse `RideQueue` with an `IsSingleRider` flag on `QueuedGroup`:* less new code, but the aggregate would carry two independent orderings and every existing member (`CanAccept`, `PeopleWaiting`, `SnapshotGroups`) would need a lane argument, silently changing the meaning of the group-loading path. Rejected.

### 2. `TakeUpTo(count)` — take individuals in bulk, not by identity

Group taking is by `GroupId` because the coordinator picks a *specific* group out of a snapshot and must be race-safe against the filler. Single riders are interchangeable: the coordinator only ever wants "the next `n`". `TakeUpTo` removes them from the front in one locked operation and returns what it got, possibly fewer, possibly none. One lock acquisition, no take-by-id retry loop, and no way to strand a half-taken rider.

- *Alternative — take one rider at a time:* a lock round-trip per seat, and an interleaving where the filler inserts between takes. Rejected.
- *Alternative — take by person number, mirroring groups:* forces the coordinator to snapshot and re-scan for items that are interchangeable by definition. Rejected.

### 3. Free-seat capacity is a new, separate measure — `EmptyGondolaCount` is untouched

`GreatMill` gains `FreeSeatCount` (unoccupied seats across all gondolas, whether or not the gondola is partly occupied) and `FreeSeats()` yielding `(Gondola, SeatPosition)` in the existing stable hub-then-gondola order. `Ride` gains:

```
SeatSingleRiders(
    IReadOnlyList<PassengerWeight> riders,
    Func<TimeSpan> restraintCloseDelay,
    Func<int, int, IReadOnlyList<int>>? selectSeats = null)
```

which seats each rider into a distinct free seat and requires `riders.Count <= FreeSeatCount`, throwing `DomainValidationException` otherwise exactly as `BoardGroup` does. `Ride.FreeSeats` today returns `EmptyGondolaCount * SeatsPerGondola` — a group-oriented figure — and is renamed `GroupBoardableSeats` so the new, literal `FreeSeatCount` can take the honest name.

Introducing a second measure rather than redefining `EmptyGondolaCount` is what keeps the relaxation surgical: group boarding keeps asking "how many *whole* gondolas are free", single-rider boarding asks "how many *seats* are free", and the no-mixing rule is relaxed by exactly one caller. Seat selection is injected the way `BoardGroup` injects gondola selection, so production randomizes which seats singles take while tests fill in natural order.

- *Alternative — let `BoardGroup` also use free seats:* would silently let two different groups share a gondola. Rejected outright; that rule is the point of the gondola-boarding capability.
- *Alternative — the coordinator computes seat coordinates and calls `BoardPassenger` per seat:* leaks seat allocation out of the aggregate and races the simulation. Rejected per ADR-0003.

### 4. Two phases in one loading pass, groups always first

`RideLoadingCoordinator.RunLoadingPassAsync` keeps its current group loop verbatim, then — still inside the same pass, still only while `Loading` — runs a top-up phase:

1. `freeSeats = _store.FreeSeatCount`; return when zero.
2. `singles = await _queueService.TakeSingleRidersAsync(rideId, freeSeats, ct)`; return when empty.
3. `_store.SeatSingleRiders(singles.Select(p => new PassengerWeight(p.WeightInKilograms)))`.

The group loop's own exit conditions are unchanged, so the phase boundary is exactly "no waiting group fits any more" — including the case where the group queue is empty. Running both phases in the same tick keeps the pass idempotent and re-entrant: a rider or group arriving mid-loading is picked up on the next tick, as today.

Because `TakeUpTo` returns at most what was asked and `SeatSingleRiders` re-validates against live capacity under the store lock, the worst case of a capacity disagreement is a validation throw rather than a lost rider. The coordinator additionally clamps the take to `Math.Min(freeSeats, _store.FreeSeatCount)` immediately before seating; the store lock is the arbiter.

- *Alternative — interleave singles with group backfill (try a group, else a single, repeat):* would fill odd seats before a fitting group had its chance, quietly changing the group procedure this change is meant to preserve. Rejected.
- *Alternative — a separate hosted service for the singles phase:* two writers racing for the same seats. Rejected.

### 5. Riders taken but not seated go back to the front of the line

If seating throws after a take, the riders have already left the queue. The coordinator returns any un-seated rider to the **front** of the singles line through `ISingleRiderQueue`-backed `RequeueSingleRidersAsync(rideId, people, ct)` on the contract, rather than dropping guests on the floor. Given the clamp in decision 4 this path is practically unreachable; it exists so that a future concurrent writer cannot silently vanish a guest, and it is cheap to keep correct now.

### 6. Queue contract and DTO layout

`IRideQueueService` gains:

- `Task<PersonDto> EnqueueSingleRiderAsync(Guid rideId, CancellationToken ct)` — generates one person, appends them to the singles line, publishes `SingleRiderQueuedIntegrationEvent`.
- `Task<IReadOnlyList<PersonDto>> TakeSingleRidersAsync(Guid rideId, int count, CancellationToken ct)` — up to `count` from the front; returns empty rather than throwing when the line is empty.
- `Task RequeueSingleRidersAsync(Guid rideId, IReadOnlyList<PersonDto> people, CancellationToken ct)` — the compensation of decision 5.

`GetStatus` reports the singles line alongside the groups. Per the repo's DTO rule the Queue contract moves under `DataTransferObjects/`, with the response in a per-feature namespace (`DataTransferObjects/GetQueueStatus/GetQueueStatusResponse.cs`) and the shapes reused across features (`PersonDto`, `QueuedGroupDto`) directly under `DataTransferObjects`. `QueueStatusDto` is superseded by:

```
GetQueueStatusResponse(
    Guid RideId,
    int GroupCount,
    int PeopleWaiting,
    IReadOnlyList<QueuedGroupDto> Groups,
    int SingleRidersWaiting,
    IReadOnlyList<PersonDto> SingleRiders)
```

`PeopleWaiting` keeps meaning *people waiting in groups*, so the group-loading path and the existing panel metric are unchanged; singles are reported as their own count, with a computed `TotalPeopleWaiting => PeopleWaiting + SingleRidersWaiting` for a headline figure.

- *Alternative — fold singles into `PeopleWaiting`:* changes the meaning of a number the coordinator and the panel already interpret. Rejected.
- *Alternative — a second endpoint `GET /rides/{id}/queue/single-riders`:* a second poll, a second failure mode, and two snapshots that can disagree about the same instant. Rejected; one snapshot, one poll.

### 7. Singles are an additional arrival stream in the existing filler

`QueueModuleOptions` gains `MinSingleArrivalsPerCycle` (default 1), `MaxSingleArrivalsPerCycle` (default 3) and `MaxSingleRiderQueueLength` (default 200). `RideQueueFillerService.FillRideAsync` plans the singles count with the same `ArrivalPlanner.PlanArrivalCount` + `ScaleForWeather` pair it already uses for groups — bad weather suppresses lone guests exactly as it suppresses parties — then enqueues that many individuals one at a time, stopping early when the singles line is at capacity. The same seeded `Random` drives both streams, so `RandomSeed` still makes a whole cycle reproducible.

Reusing the existing hosted service and planner keeps one clock, one weather read and one cycle; the two streams stay independent in *volume* only.

- *Alternative — a second hosted service:* a second timer, a second weather read, and cycles that drift apart. Rejected.
- *Alternative — split the existing headcount budget between groups and singles:* silently reduces group arrivals and changes the behaviour of a queue that is already tuned. Rejected.

### 8. `single-rider-queued` integration event

A new `SingleRiderQueuedIntegrationEvent(Guid RideId, long PersonNumber, DateTimeOffset QueuedAt)` marked `[TopicName("single-rider-queued")]`, mirroring `group-queued`. Like `group-queued` it has no subscriber yet; publishing it keeps the two arrival streams symmetric and gives a future low-latency loading trigger something to hang on.

### 9. Frontend: one more metric on the same feed

`QueueStatusDto`/`QueueStatus` in `queue.models.ts` gain `singleRidersWaiting`; `toQueueStatus` maps it; `summaryText` extends to, for example, `"12 groups queued, 34 people waiting, 5 single riders."`, pluralized through the existing `pluralize`; `QueueStateService` gains a `singleRidersWaiting` computed; `QueuePanel` gains a third metric row. No new source, no new poll, no new state — `HttpQueueSource` already carries the field once the server sends it. `FakeQueueSource`'s `createQueueStatus` gains the field so existing specs keep compiling.

## Risks / Trade-offs

- **Strangers now share a gondola** → the relaxation is real and deliberate; the safety model does not care who sits where, but a display might imply group membership. Mitigation: the rule is relaxed only inside `SeatSingleRiders`; `BoardGroup` is untouched, so a group is still never split or joined. Distinguishing "party" from "paired stranger" in the gondola panel is a follow-up display concern.
- **`Ride.FreeSeats` is renamed** → a compile-breaking rename inside DigitalTwin. Mitigation: it is module-internal, not on `.Abstractions`; the compiler finds every caller, and the rename is what stops a future reader grabbing the group-oriented number for a seat-level decision.
- **`QueueStatusDto` → `GetQueueStatusResponse` touches every consumer at once** → the DTO relocation and the shape change land together. Mitigation: only two consumers exist (the coordinator and the Angular panel) and both are in this change; doing the relocation now avoids reshaping the same contract twice.
- **Singles can starve behind groups** → while groups keep fitting, no single boards. Mitigation: accepted and intended — group consistency keeps priority by request. The singles line has its own cap so a starved line stops growing rather than growing without bound.
- **Odd seats are only filled when singles are waiting** → with an empty singles line the ride departs with the same stranded seats as today. Accepted; the arrival stream keeps the line stocked.
- **Two takes per pass at ~120 Hz** → one extra in-memory snapshot and at most one bulk take per tick. Mitigation: the top-up phase exits immediately when `FreeSeatCount` is zero or the singles line is empty, matching the existing fast-path discipline.

## Migration Plan

Additive and in-process; no data migration, since both queues and the ride are ephemeral in-memory state. Order:

1. **IntegrationMessages**: add `SingleRiderQueuedIntegrationEvent`.
2. **Queue abstractions**: relocate the existing DTOs into `DataTransferObjects/`, add `GetQueueStatusResponse` with the singles fields, extend `IRideQueueService` with the singles operations.
3. **Queue module**: add `SingleRiderQueue`, extend the store, implement the new contract members on `RideQueueService`, extend `QueueModuleOptions` and `RideQueueFillerService` with the singles stream. Unit-test the aggregate, the take semantics and the arrival stream.
4. **DigitalTwin domain**: add `GreatMill.FreeSeatCount`/`FreeSeats()`, rename `Ride.FreeSeats` → `GroupBoardableSeats`, add `Ride.SeatSingleRiders`. Unit-test stranger pairing, the capacity guard and seat selection.
5. **DigitalTwin application**: expose `FreeSeatCount` and `SeatSingleRiders` on `IRideStore`/`RideStore` under the store lock; add the top-up phase to `RideLoadingCoordinator`. Unit-test phase order and the groups-first boundary.
6. **Frontend**: extend the queue model, state service, panel and their specs/fakes.
7. `dotnet build` and `dotnet test` green (module coverage at or above 80%), `npm test` green.

Rollback is removing the top-up phase call from the loading pass and the singles stream from the filler; the new aggregate, contract members and domain methods are inert when unused. The DTO relocation is the only step that cannot be partially rolled back, which is why it lands first and independently of behaviour.

## Open Questions

- Should a waiting **group of one** be offered the singles line, or auto-promoted into it, when the group line stalls? Assumed no — a party of one never consented to sharing a gondola.
- Should the singles top-up also respect the **3200 kg** limit? Assumed no, consistent with group loading; the existing `Overloaded` guard still blocks `Loading → Safe`.
- Should the frontend list waiting single riders by name, as it could for groups, or is the count enough? Assumed the count, matching the panel's at-a-glance design.
- Is 1–3 singles per cycle the right rate against 4–8 group arrivals? Assumed yes as a starting default; it is configurable.
