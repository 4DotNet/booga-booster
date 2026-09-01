## 1. Queue: make the line drainable

- [x] 1.1 Add `QueuedGroup? Remove(Guid groupId)` to `RideQueue` (Domain/RideQueue.cs) — removes and returns that group under the existing `_gate` lock, marks the aggregate `Modified`, returns `null` when the group is not present.
- [x] 1.2 Add `Task<QueuedGroupDto?> TakeGroupAsync(Guid rideId, Guid groupId, CancellationToken)` to `IRideQueueService` (Queue.Abstractions) with XML docs describing take-by-id semantics and the `null`/already-gone result.
- [x] 1.3 Implement `TakeGroupAsync` in `RideQueueService` — resolve the queue via the store, call `RideQueue.Remove`, and map the removed `QueuedGroup` to `QueuedGroupDto` (or return `null`).
- [x] 1.4 Unit-test the queue changes (xunit.v3 / Bogus): remove-by-id returns the group and shrinks the line; removing a non-front group preserves the order of the rest; removing an absent/already-taken id returns `null`; concurrent enqueue + remove keeps ordering intact.

## 2. DigitalTwin domain: board a whole group

- [x] 2.1 Expose the ride's free capacity in whole gondolas — add an `EmptyGondolaCount` (and/or `FreeSeats = 2 * EmptyGondolaCount`) read path on `GreatMill`/`Ride` that counts gondolas with `IsEmpty == true`.
- [x] 2.2 Add `Ride.BoardGroup(IReadOnlyList<PassengerWeight> members, Func<TimeSpan> restraintCloseDelay)` (or equivalent) that: allows only `Idle`/`Loading`; verifies `ceil(N/2) <= EmptyGondolaCount` and throws `DomainValidationException` otherwise; allocates that many empty gondolas; seats members two-per-gondola with the odd member alone (never mixing groups in a gondola); forces state to `Loading`; `MarkChanged()`.
- [x] 2.3 Unit-test `BoardGroup` (xunit.v3): pairs seat together; a group of 3 takes 2 gondolas with the odd member alone; boarding is rejected when `ceil(N/2)` exceeds empty gondolas and seats no one; boarding is rejected outside `Idle`/`Loading`; members' weights count toward `PassengerLoadKg`.

## 3. DigitalTwin application: the loading coordinator

- [x] 3.1 Add a `Queue.Abstractions` project reference to `FourDotnet.BoogaBooster.DigitalTwin` (ADR-0004 — abstractions only).
- [x] 3.2 Add `RideTelemetry BoardGroup(...)` and a free-capacity accessor to `IRideStore`/`RideStore`, running under the store `_gate` lock and mapping queue `PersonDto` weights (int kg) to `PassengerWeight` (double).
- [x] 3.3 Implement `RideLoadingCoordinator` — a single idempotent `RunLoadingPass(rideId)` that: no-ops unless the ride is `Loading`; reads free-gondola capacity; peeks the first three waiting groups via `IRideQueueService.GetStatus`; loops boarding the first of positions 1–3 that fits (`TakeGroupAsync` then `IRideStore.BoardGroup`), re-evaluating capacity after each; stops when no group in the window fits or capacity is exhausted; treats a `null` take result as skip-and-rescan.
- [x] 3.4 Invoke `RunLoadingPass` from the ride's advance path (e.g. within `RideStore.Advance` / `RideSimulationService`) while `state == Loading`, with a fast exit when not `Loading` / queue empty / no empty gondola.
- [x] 3.5 Register `RideLoadingCoordinator` in `DigitalTwinModuleExtensions` and wire it into the advance path.

## 4. Behavior tests for the loading algorithm

- [x] 4.1 Fit + drain: consecutive fitting groups all board and leave the queue empty; boarding advances the front of the line (Moq `IRideQueueService`, Bogus groups).
- [x] 4.2 Look-ahead/backfill: a too-large front group is skipped so the second (then third) group boards while the front group stays queued in order; backfilling continues while capacity and fitting groups remain.
- [x] 4.3 Full: none of the first three groups fit → nothing dequeued and the ride is full; no empty gondola → immediately full; a fitting fourth group is not reached.
- [x] 4.4 Mid-loading arrivals: a newly enqueued fitting group boards on the next pass; an oversized arrival stays queued while full; an arrival after the ride leaves `Loading` is not boarded.
- [x] 4.5 State gating: no boarding while `Idle`, `Safe`, or `Started`; loading an empty queue seats no one and leaves the ride unchanged.

## 5. Verify

- [x] 5.1 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` are green. _Build clean; all new tests green (Queue 77, DigitalTwin 114). One **pre-existing, unrelated** failure remains — `ValueObjectTests.PassengerWeight_rejects_out_of_range_values(130.1)` expects rejection but `RideParameters.MaxPassengerKg` is `150`; confirmed failing on a clean tree without this change._
- [x] 5.2 Manual/Aspire smoke check: with the queue filling, drive `Idle → Loading` and confirm groups drain into seats until the ride reports full, and a group arriving mid-loading boards immediately. _Deferred — requires running the Aspire AppHost with the Dapr sidecars + RabbitMQ; a manual step for the user._
