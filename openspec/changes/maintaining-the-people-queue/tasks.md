## 1. Contract (Queue.Abstractions)

- [ ] 1.1 Add DTOs to `FourDotnet.BoogaBooster.Queue.Abstractions`: `GuestDto`, `GroupArrivalDto`, `QueuedGroupDto`, `QueueStatusDto`, `GondolaAssignmentDto`, `BoardingResultDto`
- [ ] 1.2 Define service interfaces in `.Abstractions`: `IRideQueueService` (enqueue, status, board-next) and the boarding result contract
- [ ] 1.3 Define the inbound dependency abstractions consumed by the module: `IWeatherConditionProvider` and `IGondolaAvailabilityProvider` (available gondola count + seat capacity)
- [ ] 1.4 Confirm `Queue.Abstractions` references only what a public contract needs (no implementation dependencies)

## 2. Shared DDD plumbing (Core)

- [ ] 2.1 Verify/implement the domain-model base class in `FourDotnet.BoogaBooster.Core` with lifecycle state (`New`/`Pristine`/`Touched`/`Modified`/`Deleted`) and the `SetX()` change-apply helpers (ADR-0003)
- [ ] 2.2 Add `Queue` module reference to `FourDotnet.BoogaBooster.Core` and to `FourDotnet.BoogaBooster.Weather.Abstractions`

## 3. Domain model (ride-queue)

- [ ] 3.1 Implement `Guest` and `QueuedGroup` (group id + ordered members) with encapsulated properties
- [ ] 3.2 Implement `GroupArrival` value object, fully validated on construction (non-empty members, consistent size/group id)
- [ ] 3.3 Implement `RideQueue` aggregate deriving from the Core base class: ordered groups, per-ride identity, lifecycle state
- [ ] 3.4 Implement `Enqueue(GroupArrival)` — appends a contiguous group at the back, marks `Modified`; enforce max-length rejection
- [ ] 3.5 Implement `PeekNextGroup()` (front group, no removal) and `TakeNextGroup(...)` used by boarding
- [ ] 3.6 Guard concurrent mutation (per-ride lock or channel) so filling and boarding cannot corrupt ordering

## 4. Boarding (gondola-boarding)

- [ ] 4.1 Implement the pair-seating algorithm: two members per gondola, odd final member alone, spread across multiple gondolas
- [ ] 4.2 Bound seating by available gondola capacity; leave the un-seated remainder as an intact group at the front of the queue
- [ ] 4.3 Produce a `BoardingResult` with gondola assignments, remainder, and the "no group available" case for an empty queue
- [ ] 4.4 Implement the empty-queue dispatch signal (dispatch allowed only when at least one gondola is occupied)
- [ ] 4.5 Wire boarding into `IRideQueueService` using `IGondolaAvailabilityProvider`

## 5. Weather-driven filling

- [ ] 5.1 Implement `IArrivalRateStrategy` mapping a weather condition to an arrival count (good → higher, bad → few/none)
- [ ] 5.2 Implement arrival generation producing a mix of lone guests and groups (seedable randomness injected)
- [ ] 5.3 Implement the `BackgroundService` fill loop using `TimeProvider` for deterministic intervals; read weather each cycle via `IWeatherConditionProvider`
- [ ] 5.4 Respect the configured maximum queue length per ride (no arrivals when at capacity)
- [ ] 5.5 Add stub `IWeatherConditionProvider`/`IGondolaAvailabilityProvider` implementations so the module runs before Weather/ride modules are built

## 6. API host wiring (minimal APIs)

- [ ] 6.1 Add DI registrations for the queue service, strategies, providers, and hosted filler in `FourDotnet.BoogaBooster.Api`
- [ ] 6.2 Register a `MapGroup("/rides/{rideId}/queue")` with minimal-API endpoints: `GET` status and `POST` board-next, returning `TypedResults` (ADR-0002)
- [ ] 6.3 Add the `Queue` module project reference to `FourDotnet.BoogaBooster.Api`

## 7. Tests (xunit.v3)

- [ ] 7.1 Create test project `FourDotnet.BoogaBooster.Queue.Tests` (`xunit.v3`, `xunit.runner.visualstudio`, `Microsoft.NET.Test.Sdk`, Moq, Bogus); no FluentAssertions
- [ ] 7.2 `RideQueue` tests: arrival ordering, per-ride isolation, group contiguity, peek, max-length, lifecycle state transitions (`Modified`/`Touched`)
- [ ] 7.3 Boarding tests: pair seating, odd-remainder single gondola, multi-gondola spread, no cross-group pairing, capacity-bounded remainder, no-gondola case, empty-queue no-op
- [ ] 7.4 Dispatch tests: partial dispatch when queue empties with occupancy; no dispatch when nothing loaded
- [ ] 7.5 Fill service tests (Moq weather + injected `TimeProvider`/seed): more arrivals in good weather, few/none in bad weather, weather re-evaluated per cycle, individuals + groups enqueued, capacity respected

## 8. Verification

- [ ] 8.1 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` pass
- [ ] 8.2 Run the Aspire AppHost and exercise the queue endpoints; confirm the queue fills over time and boarding returns valid gondola assignments
- [ ] 8.3 Re-check compliance against the `4dotnet-csharp-style-guide` MCP server (DDD, minimal APIs, abstractions-only cross-module refs, test stack)
