## Scope delivered in this pass

This pass implemented the **queue store + background filler + integration-event
publishing** slice requested for `maintaining-the-people-queue`, and deferred the
**gondola boarding** and **weather-driven arrival rate** to later passes. Two
requirements were added beyond the original artifacts and are reflected in the
specs:

- Enqueuing a group publishes a `GroupQueued` integration event (RideId, GroupId,
  people count) — see `specs/ride-queue`.
- A configurable **fixed** default arrival rate (4–8 people/min) stands in for the
  weather-driven rate until the Weather module is integrated — see
  `specs/weather-driven-queue-fill`.

Boxes below are checked only where **fully** delivered; partial/deferred items are
annotated. Verified via `FourDotnet.BoogaBooster.Queue.Tests` (22 tests passing).

> Note: the whole-solution `dotnet build`/`dotnet test` (8.1) and AppHost run (8.2)
> are currently blocked by an **unrelated, pre-existing** compile error in the
> in-progress Weather module (`WeatherStore.cs`: the domain type `Weather`
> collides with the `...Weather` namespace). The Queue projects build and test
> green in isolation.

## 1. Contract (Queue.Abstractions)

- [ ] 1.1 Add DTOs to `FourDotnet.BoogaBooster.Queue.Abstractions`: `GuestDto`, `GroupArrivalDto`, `QueuedGroupDto`, `QueueStatusDto`, `GondolaAssignmentDto`, `BoardingResultDto` — _partial: `QueuedGroupDto` + `QueueStatusDto` added; boarding DTOs deferred_
- [ ] 1.2 Define service interfaces in `.Abstractions`: `IRideQueueService` (enqueue, status, board-next) and the boarding result contract — _partial: `IRideQueueService` with enqueue + status added; board-next deferred_
- [ ] 1.3 Define the inbound dependency abstractions consumed by the module: `IWeatherConditionProvider` and `IGondolaAvailabilityProvider` (available gondola count + seat capacity) — _deferred (weather/boarding)_
- [x] 1.4 Confirm `Queue.Abstractions` references only what a public contract needs (no implementation dependencies)

## 2. Shared DDD plumbing (Core)

- [x] 2.1 Verify/implement the domain-model base class in `FourDotnet.BoogaBooster.Core` with lifecycle state (`New`/`Pristine`/`Touched`/`Modified`/`Deleted`) and the `SetX()` change-apply helpers (ADR-0003)
- [ ] 2.2 Add `Queue` module reference to `FourDotnet.BoogaBooster.Core` and to `FourDotnet.BoogaBooster.Weather.Abstractions` — _partial: Core referenced; Weather.Abstractions deferred_

## 3. Domain model (ride-queue)

- [x] 3.1 Implement `Guest` and `QueuedGroup` (group id + ordered members) with encapsulated properties
- [x] 3.2 Implement `GroupArrival` value object, fully validated on construction (non-empty members, consistent size/group id)
- [x] 3.3 Implement `RideQueue` aggregate deriving from the Core base class: ordered groups, per-ride identity, lifecycle state
- [x] 3.4 Implement `Enqueue(GroupArrival)` — appends a contiguous group at the back, marks `Modified`; enforce max-length rejection
- [ ] 3.5 Implement `PeekNextGroup()` (front group, no removal) and `TakeNextGroup(...)` used by boarding — _partial: `PeekNextGroup()` done; `TakeNextGroup` deferred (boarding)_
- [x] 3.6 Guard concurrent mutation (per-ride lock or channel) so filling and boarding cannot corrupt ordering

## 4. Boarding (gondola-boarding) — deferred to a later pass

- [ ] 4.1 Implement the pair-seating algorithm: two members per gondola, odd final member alone, spread across multiple gondolas
- [ ] 4.2 Bound seating by available gondola capacity; leave the un-seated remainder as an intact group at the front of the queue
- [ ] 4.3 Produce a `BoardingResult` with gondola assignments, remainder, and the "no group available" case for an empty queue
- [ ] 4.4 Implement the empty-queue dispatch signal (dispatch allowed only when at least one gondola is occupied)
- [ ] 4.5 Wire boarding into `IRideQueueService` using `IGondolaAvailabilityProvider`

## 5. Queue filling

- [ ] 5.1 Implement `IArrivalRateStrategy` mapping a weather condition to an arrival count (good → higher, bad → few/none) — _deferred (weather); replaced this pass by the fixed 4–8/min default in `ArrivalPlanner`_
- [x] 5.2 Implement arrival generation producing a mix of lone guests and groups (seedable randomness injected)
- [ ] 5.3 Implement the `BackgroundService` fill loop using `TimeProvider` for deterministic intervals; read weather each cycle via `IWeatherConditionProvider` — _partial: `RideQueueFillerService` + `TimeProvider`-driven `PeriodicTimer` done; per-cycle weather read deferred_
- [x] 5.4 Respect the configured maximum queue length per ride (no arrivals when at capacity)
- [ ] 5.5 Add stub `IWeatherConditionProvider`/`IGondolaAvailabilityProvider` implementations so the module runs before Weather/ride modules are built — _deferred (weather/boarding)_

## 6. API host wiring (minimal APIs)

- [x] 6.1 Add DI registrations for the queue service, store, options, and hosted filler in `FourDotnet.BoogaBooster.Api` (via `AddQueueModule()`, ADR-0007) — _strategies/providers deferred_
- [ ] 6.2 Register a `MapGroup("/rides/{rideId}/queue")` with minimal-API endpoints: `GET` status and `POST` board-next, returning `TypedResults` (ADR-0002) — _partial: `GET` status done via `MapQueueEndpoints()`; board-next deferred_
- [x] 6.3 Add the `Queue` module project reference to `FourDotnet.BoogaBooster.Api`

## 7. Tests (xunit.v3)

- [x] 7.1 Create test project `FourDotnet.BoogaBooster.Queue.Tests` (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, Moq, Bogus); no FluentAssertions
- [x] 7.2 `RideQueue` tests: arrival ordering, per-ride isolation, group contiguity, peek, max-length, lifecycle state transitions (`Modified`/`Touched`) — _Core state transitions covered in `DomainModelStateTests`_
- [ ] 7.3 Boarding tests — _deferred (boarding)_
- [ ] 7.4 Dispatch tests — _deferred (boarding)_
- [ ] 7.5 Fill service tests — _partial: `ArrivalPlanner` (rate/partition/determinism) and `RideQueueService` (publishes `GroupQueued`, status, validation) covered; full filler-loop-with-FakeTimeProvider + weather re-evaluation deferred_

## 8. Verification

- [ ] 8.1 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` pass — _Queue projects build + 22 tests pass in isolation; full-solution build blocked by the unrelated pre-existing Weather module compile error_
- [ ] 8.2 Run the Aspire AppHost and exercise the queue endpoints — _blocked by the same Weather build error (the API references the Weather module)_
- [x] 8.3 Re-check compliance against the `4dotnet-csharp-style-guide` MCP server (DDD, minimal APIs, abstractions-only cross-module refs, test stack) — _consulted ADR-0002/0003/0004/0006/0007 + unit-testing guideline throughout_

## 9. Additions delivered this pass (beyond original artifacts)

- [x] 9.1 Add `GroupQueuedIntegrationEvent` (`[TopicName("group-queued")]`, RideId/GroupId/PeopleCount/QueuedAt) to `FourDotnet.BoogaBooster.IntegrationMessages`
- [x] 9.2 Publish the `GroupQueued` event from `RideQueueService.EnqueueGroupAsync` on every successful enqueue via `IIntegrationEventPublisher`
- [x] 9.3 Provide the fixed, configurable 4–8 people/min default arrival rate (`QueueModuleOptions` + `ArrivalPlanner`) as a stand-in for the weather-driven rate
