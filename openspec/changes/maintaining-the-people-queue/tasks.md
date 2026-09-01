## Scope delivered

This change delivers the **queue store + background filler + integration-event
publishing** slice for `maintaining-the-people-queue`. Two requirements were added
beyond the original artifacts and are reflected in the specs:

- Enqueuing a group publishes a `GroupQueued` integration event (RideId, GroupId,
  people count) — see `specs/ride-queue`.
- A configurable **fixed** default arrival rate (4–8 people/min) stands in for the
  weather-driven rate until the Weather module is integrated — see
  `specs/weather-driven-queue-fill`.

### How later changes overtook this one

Several tasks below were written before `weather-service`, `queue-depend-on-weather`,
`loading-the-ride` and `connect-data-panels` shipped, and those changes solved the
same problems differently. Such tasks are checked with a **superseded-by** note
pointing at what actually implements them; nothing was left unbuilt behind a tick.

- **Weather.** The Queue module consumes weather by subscribing to
  `WeatherUpdateIntegrationEvent` (`WeatherSubscriptionEndpoints` → `IWeatherInfluence`
  → `ArrivalPlanner.ScaleForWeather`), not by polling `IWeatherConditionProvider`. The
  module therefore needs **no** project reference to `Weather.Abstractions`, and no
  `IArrivalRateStrategy` or stub providers.
- **Boarding.** Boarding lives in the **DigitalTwin** module (`RideLoadingCoordinator`
  + `Ride.BoardGroup`), which owns gondola state, and is specified by the
  `ride-loading` capability. The Queue module's part of the contract is
  `TakeGroupAsync(rideId, groupId)` — removal by identity, which is race-safe and
  lets the loader look ahead past a group that does not fit.
- **The fully-fits rule.** `specs/gondola-boarding` originally required *partial*
  boarding, with the un-seated remainder left at the front of the queue. That
  contradicted the shipped `ride-loading` capability ("a group is boarded only when it
  fully fits"). The spec has been rewritten to the all-or-nothing rule, plus a new
  requirement that a party larger than the ride's 32 seats is **split on arrival** —
  otherwise it could never satisfy the fully-fits rule and would wait forever.

Verified via `dotnet build BoogaBooster.slnx` (0 errors) and the full test suite:
**370 tests, 0 failures** (Queue: 105).

## 1. Contract (Queue.Abstractions)

- [x] 1.1 Add DTOs to `FourDotnet.BoogaBooster.Queue.Abstractions`: `GuestDto`, `GroupArrivalDto`, `QueuedGroupDto`, `QueueStatusDto`, `GondolaAssignmentDto`, `BoardingResultDto` — _`PersonDto` (named for the domain's `Person`), `QueuedGroupDto` and `QueueStatusDto` shipped; the boarding DTOs are **superseded** — boarding is owned by DigitalTwin, which needs no Queue-side boarding contract_
- [x] 1.2 Define service interfaces in `.Abstractions`: `IRideQueueService` (enqueue, status, board-next) and the boarding result contract — _`EnqueueGroupAsync`, `GetStatus` and `TakeGroupAsync` shipped; board-next is **superseded** by `TakeGroupAsync`, which removes a caller-chosen group by id_
- [x] 1.3 Define the inbound dependency abstractions consumed by the module: `IWeatherConditionProvider` and `IGondolaAvailabilityProvider` (available gondola count + seat capacity) — _`IWeatherConditionProvider` exists in `Weather.Abstractions`; both are **superseded** for Queue — weather arrives as an integration event and gondola availability is read by DigitalTwin via `IRideStore.EmptyGondolaCount`_
- [x] 1.4 Confirm `Queue.Abstractions` references only what a public contract needs (no implementation dependencies)

## 2. Shared DDD plumbing (Core)

- [x] 2.1 Verify/implement the domain-model base class in `FourDotnet.BoogaBooster.Core` with lifecycle state (`New`/`Pristine`/`Touched`/`Modified`/`Deleted`) and the `SetX()` change-apply helpers (ADR-0003)
- [x] 2.2 Add `Queue` module reference to `FourDotnet.BoogaBooster.Core` and to `FourDotnet.BoogaBooster.Weather.Abstractions` — _Core referenced; the `Weather.Abstractions` reference is **superseded** — the event-driven weather hand-off removes the compile-time coupling entirely_

## 3. Domain model (ride-queue)

- [x] 3.1 Implement `Guest` and `QueuedGroup` (group id + ordered members) with encapsulated properties
- [x] 3.2 Implement `GroupArrival` value object, fully validated on construction (non-empty members, consistent size/group id)
- [x] 3.3 Implement `RideQueue` aggregate deriving from the Core base class: ordered groups, per-ride identity, lifecycle state
- [x] 3.4 Implement `Enqueue(GroupArrival)` — appends a contiguous group at the back, marks `Modified`; enforce max-length rejection
- [x] 3.5 Implement `PeekNextGroup()` (front group, no removal) and `TakeNextGroup(...)` used by boarding — _`PeekNextGroup()` shipped; `TakeNextGroup` **superseded** by `Remove(groupId)`, which is race-safe against concurrent filling and supports the loader's look-ahead_

## 4. Boarding (gondola-boarding)

- [x] 4.1 Implement the pair-seating algorithm: two members per gondola, odd final member alone, spread across multiple gondolas — _**superseded**: implemented in `DigitalTwin.Domain.Ride.BoardGroup`, which seats a group of N into exactly `ceil(N / 2)` empty gondolas and never shares a gondola between groups_
- [x] 4.2 ~~Bound seating by available gondola capacity; leave the un-seated remainder as an intact group at the front of the queue~~ **Rule changed** — a group is boarded only when it *fully* fits and is never split across passes. A party larger than the ride's 32 seats is split **on arrival** instead, so every waiting group stays boardable: `GroupArrival.PartitionSizes`, enforced by `RideQueue.MaxBoardableGroupSize` and applied in `RideQueueService.EnqueueGroupAsync`
- [x] 4.3 Produce a `BoardingResult` with gondola assignments, remainder, and the "no group available" case for an empty queue — _**superseded**: with all-or-nothing boarding there is no remainder to report; `RideLoadingCoordinator` reads `IRideStore.EmptyGondolaCount` and treats an empty line as "no group available"_
- [x] 4.4 Implement the empty-queue dispatch signal (dispatch allowed only when at least one gondola is occupied) — _**superseded** by the `ride-state-machine` capability, which governs `Loading → Safe → Started`_
- [x] 4.5 Wire boarding into `IRideQueueService` using `IGondolaAvailabilityProvider` — _**superseded**: `RideLoadingCoordinator` drives boarding from the DigitalTwin side each simulation tick, taking groups through `IRideQueueService.TakeGroupAsync`_

## 5. Queue filling

- [x] 5.1 Implement `IArrivalRateStrategy` mapping a weather condition to an arrival count (good → higher, bad → few/none) — _**superseded** by `ArrivalPlanner.ScaleForWeather` (`Ceiling × NiceWeather^Exponent`, flooring to zero in the worst weather) fed by `IWeatherInfluence`_
- [x] 5.2 Implement arrival generation producing a mix of lone guests and groups (seedable randomness injected)
- [x] 5.3 Implement the `BackgroundService` fill loop using `TimeProvider` for deterministic intervals; read weather each cycle via `IWeatherConditionProvider` — _`RideQueueFillerService` uses a `TimeProvider`-driven `PeriodicTimer` and reads `IWeatherInfluence.Current` every cycle_
- [x] 5.4 Respect the configured maximum queue length per ride (no arrivals when at capacity)
- [x] 5.5 Add stub `IWeatherConditionProvider`/`IGondolaAvailabilityProvider` implementations so the module runs before Weather/ride modules are built — _**superseded**: both real integrations shipped, so no stubs are needed; `RideLoadingCoordinator` already treats an absent Queue module as a no-op_

## 6. API host wiring (minimal APIs)

- [x] 6.1 Add DI registrations for the queue service, store, options, and hosted filler in `FourDotnet.BoogaBooster.Api` (via `AddQueueModule()`, ADR-0007)
- [x] 6.2 Register a `MapGroup("/rides/{rideId}/queue")` with minimal-API endpoints: `GET` status and `POST` board-next, returning `TypedResults` (ADR-0002) — _`GET` status shipped via `MapQueueEndpoints()`; board-next is **superseded** — the loading pass runs on the simulation tick, so no HTTP trigger is needed_
- [x] 6.3 Add the `Queue` module project reference to `FourDotnet.BoogaBooster.Api`

## 7. Tests (xunit.v3)

- [x] 7.1 Create test project `FourDotnet.BoogaBooster.Queue.Tests` (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, Moq, Bogus); no FluentAssertions
- [x] 7.2 `RideQueue` tests: arrival ordering, per-ride isolation, group contiguity, peek, max-length, lifecycle state transitions (`Modified`/`Touched`) — _Core state transitions covered in `DomainModelStateTests`_
- [x] 7.3 Boarding tests — _`RideLoadingCoordinatorTests` in the DigitalTwin suite covers the loading pass; `OversizedGroupSplittingTests` covers the Queue side of the fully-fits rule (splitting, the `MaxBoardableGroupSize` invariant, and all-or-nothing batch admission)_
- [x] 7.4 Dispatch tests — _**superseded**: dispatch is the ride state machine's, covered by the DigitalTwin suite_
- [x] 7.5 Fill service tests — _`RideQueueFillerServiceTests` drives the loop with `FakeTimeProvider`, `WeatherSubscriptionTests` covers weather re-evaluation, and `ArrivalPlannerTests` covers rate/partition/determinism_

## 8. Verification

- [x] 8.1 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` pass — _build: 0 errors. Tests: 370 passing, 0 failing (Core 17, Queue 105, IntegrationMessages 15, Weather 70, DigitalTwin 163). The Weather compile error that blocked this earlier is gone. Fixed a stale `ValueObjectTests` case asserting a 130 kg passenger cap that `RideParameters.MaxPassengerKg` had already widened to 150. Note: `dotnet test` itself needs the .NET 10 Microsoft.Testing.Platform opt-in, which this repo has not set — the suites were run as executables._
- [ ] 8.2 Run the Aspire AppHost and exercise the queue endpoints — _still outstanding: requires a human run with the Dapr sidecars + RabbitMQ_
- [x] 8.3 Re-check compliance against the `4dotnet-csharp-style-guide` MCP server (DDD, minimal APIs, abstractions-only cross-module refs, test stack) — _consulted ADR-0002/0003/0004/0006/0007 + unit-testing guideline throughout; ADR-0003 re-consulted for the splitting work_

## 9. Additions delivered beyond the original artifacts

- [x] 9.1 Add `GroupQueuedIntegrationEvent` (`[TopicName("group-queued")]`, RideId/GroupId/PeopleCount/QueuedAt) to `FourDotnet.BoogaBooster.IntegrationMessages`
- [x] 9.2 Publish the `GroupQueued` event from `RideQueueService.EnqueueGroupAsync` on every successful enqueue via `IIntegrationEventPublisher`
- [x] 9.3 Provide the fixed, configurable 4–8 people/min default arrival rate (`QueueModuleOptions` + `ArrivalPlanner`) as a stand-in for the weather-driven rate
- [x] 9.4 Split a party larger than the ride's seat capacity into the smallest number of evenly sized boardable groups on arrival (`GroupArrival.PartitionSizes`, `QueueModuleOptions.MaxBoardableGroupSize` defaulting to 32), enqueued atomically via `RideQueue.EnqueueAll` so a split party is never half-admitted
