## 1. Integration message

- [ ] 1.1 Add `SingleRiderQueuedIntegrationEvent(Guid RideId, long PersonNumber, DateTimeOffset QueuedAt)` in `src/Shared/FourDotnet.BoogaBooster.IntegrationMessages/Events/Queue/`, marked `[TopicName("single-rider-queued")]` and implementing `IIntegrationEvent`, mirroring `GroupQueuedIntegrationEvent`.

## 2. Queue abstractions — DTO layout and contract

- [ ] 2.1 Create `DataTransferObjects/` in `FourDotnet.BoogaBooster.Queue.Abstractions` and move `PersonDto` and `QueuedGroupDto` into it (namespace `...Queue.Abstractions.DataTransferObjects`), one type per file.
- [ ] 2.2 Add `DataTransferObjects/GetQueueStatus/GetQueueStatusResponse.cs` with `RideId`, `GroupCount`, `PeopleWaiting`, `Groups`, `SingleRidersWaiting`, `SingleRiders` and a computed `TotalPeopleWaiting`; delete `QueueStatusDto.cs`.
- [ ] 2.3 Extend `IRideQueueService` with `EnqueueSingleRiderAsync(Guid rideId, CancellationToken)`, `TakeSingleRidersAsync(Guid rideId, int count, CancellationToken)` and `RequeueSingleRidersAsync(Guid rideId, IReadOnlyList<PersonDto> people, CancellationToken)`, and change `GetStatus` to return `GetQueueStatusResponse`; document the take's "up to count, possibly empty, never throws" and the requeue's front-of-line semantics in XML docs.
- [ ] 2.4 Fix up the resulting compile errors in existing consumers (`QueueEndpoints`, `RideQueueService`, `RideLoadingCoordinator`, existing tests) so the solution builds before any behaviour is added.

## 3. Queue domain — the single-riders line

- [ ] 3.1 Add `Domain/SingleRiderQueue.cs`: aggregate root deriving from `DomainModel`, validating `rideId` and `maxPeople` with `DomainValidationException`, holding a `LinkedList<Person>` behind a `Lock`, exposing `RideId`, `MaxPeople`, `PeopleWaiting`, `CanAccept(int)`, `Enqueue(Person)`, `TakeUpTo(int)`, `RequeueFront(IReadOnlyList<Person>)` and `SnapshotPeople()`.
- [ ] 3.2 Make `TakeUpTo` return an empty list for a count of zero or less and for an empty line, return everyone when asked for more than are waiting, and mark the aggregate changed only when it actually removed someone.
- [ ] 3.3 Make `Enqueue` throw `DomainValidationException` when the line is at `MaxPeople`, leaving the waiting people untouched.
- [ ] 3.4 Make `RequeueFront` insert the given people at the head in their original relative order.

## 4. Queue infrastructure and service

- [ ] 4.1 Add `MaxSingleRiderQueueLength` (default 200), `MinSingleArrivalsPerCycle` (default 1) and `MaxSingleArrivalsPerCycle` (default 3) to `QueueModuleOptions` with XML docs.
- [ ] 4.2 Extend `IRideQueueStore` and `InMemoryRideQueueStore` with `GetOrCreateSingles(Guid)` / `FindSingles(Guid)` backed by a second `ConcurrentDictionary`, sized from `MaxSingleRiderQueueLength`.
- [ ] 4.3 Implement `EnqueueSingleRiderAsync` on `RideQueueService`: generate one person via `IPersonGenerator.Next()`, enqueue it, log the arrival, publish `SingleRiderQueuedIntegrationEvent` and return the `PersonDto`.
- [ ] 4.4 Implement `TakeSingleRidersAsync` and `RequeueSingleRidersAsync` on `RideQueueService`, mapping between `Person` and `PersonDto` and returning an empty list when the ride has no singles line yet.
- [ ] 4.5 Extend `GetStatus` to report `SingleRidersWaiting` and `SingleRiders` from the singles line, leaving `GroupCount` and `PeopleWaiting` counting groups only.

## 5. Queue background filler

- [ ] 5.1 In `RideQueueFillerService.FillRideAsync`, plan the singles headcount with `ArrivalPlanner.PlanArrivalCount(MinSingleArrivalsPerCycle, MaxSingleArrivalsPerCycle, _rng)` followed by `ArrivalPlanner.ScaleForWeather(...)`, using the same `_rng` and the same weather read as the group stream.
- [ ] 5.2 Enqueue that many individuals one at a time via `EnqueueSingleRiderAsync`, breaking out early (with a debug log, like the group path) when the singles line reports it cannot accept another person.
- [ ] 5.3 Verify the group arrival planning is untouched — the singles stream is planned and enqueued after the groups and shares no budget with them.

## 6. Queue tests

- [ ] 6.1 Add `SingleRiderQueueTests`: arrival order, `TakeUpTo` for fewer/more/zero/empty, capacity refusal, `RequeueFront` ordering, and validation on construction.
- [ ] 6.2 Extend `RideQueueServiceTests`: enqueueing a single rider publishes `single-rider-queued`, taking returns and removes from the front, requeueing restores the front, and `GetStatus` reports both lines with group counts unaffected by singles.
- [ ] 6.3 Extend the filler tests: individuals are added each cycle within bounds, worst weather adds none, the singles capacity stops the stream early, and group arrivals are unchanged with the singles stream enabled (seeded `RandomSeed` for determinism).
- [ ] 6.4 Run `dotnet test` for the Queue tests and confirm the module stays at or above 80% line coverage.

## 7. DigitalTwin domain — free seats and stranger pairing

- [ ] 7.1 Add `FreeSeatCount` and `FreeSeats()` (yielding gondola/seat pairs in the existing stable hub-then-gondola order) to `GreatMill`, counting every unoccupied seat regardless of whether the gondola is partly occupied.
- [ ] 7.2 Rename `Ride.FreeSeats` to `Ride.GroupBoardableSeats` (still `EmptyGondolaCount * SeatsPerGondola`) and update every caller; add `Ride.FreeSeatCount` delegating to the mill.
- [ ] 7.3 Add `Ride.SeatSingleRiders(IReadOnlyList<PassengerWeight> riders, Func<TimeSpan> restraintCloseDelay, Func<int, int, IReadOnlyList<int>>? selectSeats = null)`: reject an empty rider list and a ride not `Idle`/`Loading`, throw `DomainValidationException` when `riders.Count > FreeSeatCount`, seat each rider into a distinct free seat, force `Loading` and mark changed.
- [ ] 7.4 Confirm `Ride.BoardGroup` and `GreatMill.EmptyGondolaCount` are unchanged, so groups still take only whole empty gondolas.
- [ ] 7.5 Add domain tests: a single rider fills the seat beside an earlier group's lone member, two singles share an empty gondola, a group is still refused a partly-occupied gondola, seating more riders than free seats throws, seat selection is honoured, and seating from a non-loadable state throws.

## 8. DigitalTwin application — the top-up phase

- [ ] 8.1 Expose `int FreeSeatCount { get; }` and `RideTelemetry SeatSingleRiders(IReadOnlyList<PassengerWeight> riders)` on `IRideStore`, implementing them in `RideStore` under the existing store lock and drawing the restraint delay and seat selection from `_sampler`.
- [ ] 8.2 Extend `RideLoadingCoordinator.RunLoadingPassAsync` with the top-up phase after the existing group loop: return unless still `Loading`; read `FreeSeatCount` and return when zero; take up to that many single riders; return when none came back.
- [ ] 8.3 Clamp the take to `Math.Min(taken.Count, _store.FreeSeatCount)` immediately before seating, and on any failure return the un-seated riders to the front of the line via `RequeueSingleRidersAsync`.
- [ ] 8.4 Log the top-up outcome (riders seated, seats still free) in the style of the existing group-boarding log, keeping the pass a silent no-op when nothing happens.
- [ ] 8.5 Leave the group loop's selection, look-ahead window and stop conditions untouched.

## 9. DigitalTwin tests

- [ ] 9.1 Extend `RideLoadingCoordinatorTests`: a fitting group boards before any single rider; the top-up runs only after no group fits; the top-up runs when the group queue is empty; every free seat is filled when riders are available; fewer riders than seats leaves seats free; nothing happens when the ride is full or the singles line is empty.
- [ ] 9.2 Add tests that no rider is taken or seated while the ride is `Idle`, `Safe` or `Started`.
- [ ] 9.3 Add a test that a rider joining mid-loading is seated on a subsequent pass, and one that a seating failure returns the riders to the front of the line.
- [ ] 9.4 Run `dotnet test BoogaBooster.slnx` and confirm the whole solution is green with DigitalTwin coverage at or above 80%.

## 10. Frontend

- [ ] 10.1 Add `singleRidersWaiting` to `QueueStatusDto` and `QueueStatus` in `queue.models.ts` and map it in `toQueueStatus`.
- [ ] 10.2 Extend `summaryText` to announce the single riders, pluralized through the existing `pluralize`.
- [ ] 10.3 Add a `singleRidersWaiting` computed to `QueueStateService`.
- [ ] 10.4 Add a "Single riders" metric row to `QueuePanel`, matching the existing metric markup and styling.
- [ ] 10.5 Add `singleRidersWaiting` to `createQueueStatus` in `testing/fake-queue-source.ts`.
- [ ] 10.6 Extend the model, state, panel and a11y specs for the new figure and summary wording; run `npm test` and confirm the panel still passes the AXE checks.

## 11. Wrap-up

- [ ] 11.1 Run the app through Aspire and confirm the queue panel shows a growing single-riders line and that the ride departs with previously stranded seats filled.
- [ ] 11.2 Re-read the change's specs against the implementation and note any scenario that ended up behaving differently, so the deltas archive accurately.
