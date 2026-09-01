## Context

The Queue module already runs a hosted `RideQueueFillerService` that, each cycle, asks `ArrivalPlanner.PlanArrivalCount` for a headcount in `[MinArrivalsPerCycle, MaxArrivalsPerCycle]`, partitions it into groups, and enqueues them up to `MaxQueueLength`. The rate is currently weather-blind.

The Weather module publishes `WeatherUpdateIntegrationEvent` (in `FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather`, topic `weather-updated`) on every weather change. It carries a `NiceWeather` float in `[0, 1]` — `1` for pleasant/moderate weather, `0` for severe conditions. The integration-messaging conventions are already established: publishers use `AddBoogaBoosterIntegrationMessages()`; subscribers declare a minimal-API endpoint annotated with Dapr's `WithTopic(...)`, and the Api host already calls `UseCloudEvents()` + `MapSubscribeHandler()` and owns the Dapr sidecar/pub-sub wiring (provisioned in the AppHost). No static subscription YAML is used.

This change closes the loop: the Queue module consumes `WeatherUpdateIntegrationEvent` and lets `NiceWeather` drive how many guests arrive.

## Goals / Non-Goals

**Goals:**

- Subscribe the Queue module to `weather-updated` using the established `WithTopic` minimal-API subscriber pattern.
- Hold the latest `NiceWeather` in thread-safe, in-memory singleton state that the filler reads each cycle.
- Scale each cycle's arrival count by a configurable multiplier derived from `NiceWeather` — from base rate down to zero in the worst weather and back up as it recovers.
- Preserve the existing group-composition and capacity invariants; keep the base bounds/interval in `QueueModuleOptions`.
- Behave sensibly before the first event via a configurable neutral default.

**Non-Goals:**

- No change to the `WeatherUpdateIntegrationEvent` contract or the Weather module.
- No persistence of weather state (in-memory only, single world).
- No use of the direct `IWeatherConditionProvider` path for this behavior — the event is the source of truth here.
- No change to the queue's HTTP read surface or boarding logic.

## Decisions

### Decision: Consume the integration event rather than call the weather provider

The filler reacts to `WeatherUpdateIntegrationEvent` (push, via Dapr pub/sub) instead of pulling `IWeatherConditionProvider` each cycle. **Why:** it keeps the modules loosely coupled through the shared integration-messages contract (no cross-module project reference), matches the event-driven direction the `weather-service` change already publishes for, and gives the filler a ready-made `NiceWeather` scalar with no weather-domain interpretation logic in the Queue module. **Alternative considered:** injecting `IWeatherConditionProvider` and deriving "niceness" in the Queue module — rejected because it duplicates weather interpretation and re-introduces a synchronous dependency the event is designed to remove.

### Decision: A dedicated singleton `WeatherInfluence` state, updated by the subscriber, read by the filler

Introduce a small thread-safe singleton (e.g. `IWeatherInfluence` / `WeatherInfluence`) exposing the current multiplier. The subscriber endpoint writes the latest clamped `NiceWeather`; the `RideQueueFillerService` reads it each cycle. Storage is a single `double` guarded for atomic read/write (e.g. `Interlocked`/`volatile` or a lock), started at the configured neutral default. **Why:** the subscriber (per-request scope) and the hosted filler (singleton) need a shared, concurrency-safe hand-off point; a purpose-built singleton is the smallest thing that models "latest observed weather" and stays trivially unit-testable. **Alternative considered:** stashing the value on the existing store or options — rejected; options are config, and the store is about queues, not weather.

### Decision: Keep the indicator→multiplier mapping in pure, testable planning logic

Extend `ArrivalPlanner` (or an adjacent pure helper) with the scaling step: `scaled = Scale(baseCount, niceWeather, options)`. The mapping is configurable via new `QueueModuleOptions` settings — at minimum a curve/exponent controlling how sharply low `NiceWeather` suppresses arrivals, and an optional ceiling multiplier for very nice weather — and clamps the result to a non-negative integer. **Why:** matches the existing pattern of keeping arrival math pure and seed-testable, and makes the "bad weather → 0, nice weather → more" behavior directly assertable. Rounding uses a deterministic rule so `NiceWeather = 0` yields exactly `0`. **Alternative considered:** a linear `count * niceWeather` with no options — rejected as too blunt; the proposal calls for a configurable, sharper response.

### Decision: Map the subscriber inside `MapQueueEndpoints`, register state in `AddQueueModule`

The subscriber endpoint is mapped alongside the existing queue endpoints so the host needs no new calls (ADR-0007: modules own their composition via the two extension methods). `AddQueueModule` registers the `WeatherInfluence` singleton. **Why:** the Api host already maps queue endpoints and runs `MapSubscribeHandler()`, so the subscription is discovered automatically; no host edits are required.

### Decision: Multiplier scales the total headcount, not group sizes

Scaling is applied to `PlanArrivalCount`'s result before `PlanGroupSizes` partitions it. **Why:** preserves the existing group-composition and capacity invariants unchanged — groups are still formed within their bounds and capped at `MaxQueueLength`; only how many people arrive changes.

## Risks / Trade-offs

- **Event delivery gaps (sidecar down, broker lag) leave stale weather** → The state simply retains the last value; the neutral default covers the pre-first-event window. Acceptable for a simulated park; no retry/staleness expiry in scope.
- **Concurrent write (subscriber) vs read (filler)** → Guard the single scalar with atomic access (`Interlocked`/lock); the value is a plain `double`, so tearing is the only concern and is cheaply avoided.
- **Rounding could make near-zero weather still enqueue one group** → Use a rounding rule that floors small scaled counts to `0` and assert the `NiceWeather = 0 → 0 arrivals` scenario explicitly.
- **Overlap with `weather-driven-queue-fill` (in `maintaining-the-people-queue`)** → That spec described weather modulation abstractly via `IWeatherConditionProvider`; this change realizes the behavior concretely through the integration event. Since no specs are archived yet, this is delivered as a new capability rather than a delta; the abstract provider path is left untouched.
- **Duplicate/out-of-order events** → Idempotent by design: each event just overwrites the latest value, and clamping guards bad payloads.

## Migration Plan

Additive only. Register the `WeatherInfluence` singleton and map the subscriber endpoint; extend `QueueModuleOptions` with multiplier settings (defaults chosen so behavior without weather matches today's base rate via the neutral default). Rollback = remove the subscriber mapping and the scaling call; the filler reverts to the fixed base rate. Requires the `integration-messages` (Dapr pub/sub) and `weather-service` (event publisher) changes to be in place for end-to-end effect.

## Open Questions

- Exact default curve/exponent and ceiling values for the multiplier response — pick sensible defaults now and tune against the running simulation.
- Whether a very-nice-weather ceiling should exceed `1.0` (allow bursts above base rate) or cap at `1.0` — default proposed: allow a modest ceiling above `1.0`, configurable.
