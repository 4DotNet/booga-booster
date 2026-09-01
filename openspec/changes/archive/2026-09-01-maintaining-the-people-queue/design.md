## Context

The backend is a .NET 10 modular monolith (ADR-0004). The `Queue` bounded context is currently an empty scaffold (`src/Queue/FourDotnet.BoogaBooster.Queue` + `.Abstractions`). This change gives it its first real behavior: a per-ride waiting line that is continuously filled by a background service (arrival rate driven by weather) and drained by a boarding operation that seats grouped guests into gondolas in pairs.

Relevant constraints from the `4dotnet-csharp-style-guide`:
- **ADR-0003 (Pragmatic DDD)**: entities are rich domain models with public getters / private setters, intent-revealing `SetX()` operations that validate before assigning, value objects for multi-value changes, and lifecycle state (`New`/`Pristine`/`Touched`/`Modified`/`Deleted`) via base classes in a shared project (`FourDotnet.BoogaBooster.Core`).
- **ADR-0004 (module structure)**: cross-module references go through `.Abstractions` only; the root API hosts the module.
- **ADR-0002 (minimal APIs)**: endpoints are minimal-API registrations, thin, delegating to services and returning `TypedResults`.
- **Unit-testing guideline**: `xunit.v3`, Moq, Bogus; no FluentAssertions.

The Weather module (also a scaffold) is the source of the current condition; boarding needs gondola availability from the ride/gondola side, which is not owned by this change.

## Goals / Non-Goals

**Goals:**
- A `RideQueue` domain model that preserves arrival order and group membership and tracks lifecycle state.
- A hosted background service that fills queues over deterministic, injectable time intervals with a weather-scaled arrival count.
- A boarding operation implementing pair-seating, odd-remainder single loads, multi-gondola spread, capacity-bounded partial boarding, and empty-queue dispatch.
- A public contract in `.Abstractions` and minimal-API endpoints on the root API.

**Non-Goals:**
- Owning gondola/ride hardware state, dispatch actuation, or seat-restraint logic — consumed behind an abstraction only.
- Persistence/database choice — the domain model exposes lifecycle state, but wiring a concrete store is deferred (an in-memory repository is sufficient for this change).
- Real guest identity, ticketing, or the actual weather-simulation algorithm inside the Weather module (only its condition is consumed).
- Frontend/visualization (covered by the separate frontend change).

## Decisions

### Domain model: `RideQueue` aggregate owning ordered `QueuedGroup`s
`RideQueue` is the aggregate root (derives from the Core domain-model base class). It holds an ordered collection of `QueuedGroup`, each carrying a group identifier and its `Guest` members. Enqueue/dequeue happen through intent-revealing methods (`Enqueue(QueuedGroup)`, `TakeNextGroup()`), never public setters, so group contiguity and ordering are invariants the model enforces. Lifecycle state comes from the base class so change-tracking is uniform.
- *Alternative considered*: a flat list of guests with a `GroupId` field. Rejected — contiguity and "board together" become service-level conventions instead of enforced model invariants.

### `GroupArrival` value object for enqueuing
Arrivals set multiple values at once (members + group id + size), so per ADR-0003 they enter through a validated value object constructed by the fill service, guaranteeing a group can never be enqueued empty or malformed.

### Boarding as a domain service returning a `BoardingResult`
A `BoardingService` (or a method on the aggregate taking available capacity) implements the seating algorithm and returns a `BoardingResult` describing gondola assignments and any un-seated remainder. Gondola availability is passed in via an `IGondolaAvailabilityProvider` abstraction; boarding stays pure and testable and does not reach into ride hardware.
- Seating rule: walk the group's members two at a time into distinct available gondolas; an odd final member takes a gondola alone. Stop when members or gondolas are exhausted; leftover members remain as an intact `QueuedGroup` at the front (re-inserted before removal is committed, or simply not removed).
- *Alternative considered*: filling gondolas greedily across group boundaries. Rejected — violates the "pairs stay within a group" requirement.

### Weather-scaled filling via a hosted `BackgroundService` with an injected clock
Filling runs in an `IHostedService`/`BackgroundService` on the API host. The cycle interval and arrival counts derive from an injected time abstraction (`TimeProvider`) and an `IArrivalRateStrategy` that maps a weather condition to an arrival count. This keeps the service deterministic under test (no real delays, no `DateTime.Now`). Weather is read each cycle through `FourDotnet.BoogaBooster.Weather.Abstractions`.
- *Alternative considered*: a timer inside the domain model. Rejected — scheduling is infrastructure, not domain; it also breaks testability and the DDD "no plumbing in models" rule.

### Contract lives in `.Abstractions`; endpoints are minimal APIs
`FourDotnet.BoogaBooster.Queue.Abstractions` exposes the service interfaces and DTOs (`QueueStatusDto`, `BoardingResultDto`, `GroupArrivalDto`) that the API and other modules consume. The root API registers a `MapGroup("/rides/{rideId}/queue")` with thin `MapGet`/`MapPost` handlers (status, board-next) delegating to the queue service and returning `TypedResults`.

## Risks / Trade-offs

- **In-memory queue state across concurrent fill + boarding** → guard `RideQueue` mutation (per-ride lock or a single-threaded channel) so the background filler and API-triggered boarding cannot corrupt ordering.
- **Weather module is still a scaffold** → depend only on a small `IWeatherConditionProvider` abstraction with a stub implementation now, so this change is not blocked and swaps cleanly later.
- **Gondola availability is externally owned** → the `IGondolaAvailabilityProvider` boundary risks drifting from the real ride model; keep the abstraction minimal (available gondola count + capacity) and validate against the ride/gondola change when it lands.
- **Deterministic testing vs. realistic arrivals** → randomness in arrivals is injected (seedable), so tests are repeatable while production can use a real random source.
- **Partial-dispatch policy is a business rule** → "may dispatch when empty" is exposed as a signal/decision, not auto-actuated, so the ride owner keeps final control.

## Migration Plan

Additive change to an empty scaffold — no data migration or breaking changes. Steps: add abstractions contract → implement domain model + services in the Queue module → register the hosted filler and endpoints in the API host → add the xUnit test project. Rollback is removing the endpoint/hosted-service registration; the module compiles standalone.

## Open Questions

- Does a ride own exactly one queue, or can a ride have multiple queues (e.g., fast-pass vs. standard)? Assumed **one queue per ride** for this change.
- Who triggers boarding — the ride's dispatch cycle (backend) or an operator action (API)? Both are supported by the same service; the trigger owner is TBD with the ride/gondola change.
- Maximum queue length and base arrival rates — assumed **configurable** with sensible defaults; exact values TBD with product.
