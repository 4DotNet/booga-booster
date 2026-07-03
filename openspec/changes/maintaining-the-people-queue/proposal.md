## Why

Every ride needs a line of waiting guests before it can load gondolas, but nothing in the backend maintains that line today — the `Queue` module is an empty scaffold. Guests arrive continuously (fewer in bad weather), often in groups that insist on riding together, and gondolas seat riders in pairs. Without a queue service that models arrivals, preserves group relationships, and boards groups across gondolas, the ride has no way to decide who rides next or when to dispatch a partially loaded ride.

## What Changes

- Introduce a **per-ride queue** domain model that holds waiting guests in arrival order while preserving each guest's **group membership**, so members of a group stay adjacent and are boarded together.
- Add a **background service** that continuously **fills each ride's queue** with newly arriving guests — sometimes individuals, sometimes groups — at an arrival rate **modulated by the current weather** (bad weather sharply reduces arrivals).
- Add a **dequeue / boarding operation** that pulls the next group from the front of the queue and **boards it across one or more available gondolas**, seating riders in **pairs** where possible, allowing a **single-person gondola** when a group has an odd remainder, and allowing the ride to **dispatch partially loaded** when the queue empties.
- Expose the queue service through **minimal-API endpoints** on the root API (inspect queue state, trigger/observe boarding) and publish its public contract through `FourDotnet.BoogaBooster.Queue.Abstractions`.

## Capabilities

### New Capabilities
- `ride-queue`: The per-ride queue domain — enqueuing arriving guests and groups, preserving arrival order and group membership relationships, and tracking queue lifecycle/state so it is clear who rides next.
- `weather-driven-queue-fill`: The background service that populates each ride's queue over time with individuals and groups, with the arrival rate scaled by the current weather condition supplied by the Weather module.
- `gondola-boarding`: The dequeue-and-board operation — taking the next group and assigning its members across available gondolas in pairs, permitting single-person loads and partial-/empty-queue dispatch.

### Modified Capabilities
<!-- None — no existing baseline specs under openspec/specs/. -->

## Impact

- **Code**: `src/Queue/FourDotnet.BoogaBooster.Queue` (domain + application logic, background service) and `src/Queue/FourDotnet.BoogaBooster.Queue.Abstractions` (public contract: DTOs, service interfaces, boarding-result types). Endpoint registration added to `src/FourDotnet.BoogaBooster.Api` (minimal APIs, per ADR-0002), which references the Queue module.
- **Cross-module dependencies**: Queue consumes weather condition via `FourDotnet.BoogaBooster.Weather.Abstractions` (abstractions-only reference per ADR-0004). Domain models derive from the DDD base classes in `FourDotnet.BoogaBooster.Core` (ADR-0003).
- **Gondola / capacity model**: Depends on gondola availability and seat capacity (2 seats per gondola) as an input to boarding; the ride/gondola state provider is treated as a dependency behind an abstraction (its ownership is out of scope for this change).
- **Runtime**: Adds a hosted `BackgroundService` to the API host for continuous queue filling; timing is driven by an injectable clock/interval so it is testable and deterministic.
- **Testing**: New xUnit (`xunit.v3`) test project for the Queue module — Moq for dependencies (weather, gondola provider, clock), Bogus for fake guests/groups; no FluentAssertions (per the unit-testing guideline).
