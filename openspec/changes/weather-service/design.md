## Context

The `Weather` module is an empty scaffold, but it is already a declared dependency of other bounded contexts: `maintaining-the-people-queue` consumes `IWeatherConditionProvider`, and `integration-messages` reserves a `WeatherChangedIntegrationEvent` originating from Weather. This change turns the scaffold into a running simulation.

The behavior is a continuously evolving single-world climate: a mean-reverting random walk around moderate defaults, perturbed by two user-initiated, time-boxed events (precipitation and strong wind) that decay back to the defaults. There is no persistence requirement — the climate is ephemeral, in-memory, and singular.

Constraints are the `4dotnet-csharp-style-guide` ADRs: modular monolith with a module + `.Abstractions` pair (ADR-0004), rich DDD domain models with private setters, intent-revealing `SetX`/operations, value objects for multi-value changes, and lifecycle state from shared base classes (ADR-0003); CQRS with hand-written base classes (ADR-0005); feature-slice organization (ADR-0006); module-owned minimal-API endpoints via `AddWeatherModule()`/`MapWeatherEndpoints()` (ADR-0002, ADR-0007); and xUnit v3 + Moq + Bogus, no FluentAssertions (unit-testing guideline).

## Goals / Non-Goals

**Goals:**

- A self-driving weather simulation that holds near moderate defaults and drifts believably on a periodic tick.
- Two user-triggered, time-boxed events (precipitation: rain/snow/hail; strong wind) with the described effects and gradual reversion.
- A read endpoint for current conditions and command endpoints to trigger each event, all mapped from the module.
- Deterministic, unit-testable evolution (seeded randomness + injected `TimeProvider`).
- The `IWeatherConditionProvider` contract other modules already expect.

**Non-Goals:**

- Persisting weather history or supporting multiple simultaneous "worlds"/locations (single in-memory instance only).
- Owning the `WeatherUpdateIntegrationEvent` **type definition** or the pub/sub transport — those belong to the `integration-messages` change (central library, Dapr/RabbitMQ wiring). This change consumes that publisher; it is a prerequisite for the publishing behavior.
- Physically accurate meteorology; the model only needs to be plausible and controllable.
- Authentication/authorization on the trigger endpoints (park-internal, out of scope).
- Frontend/visualization work.

## Decisions

### Single rich `Weather` aggregate advanced by a pure `Advance` step

One aggregate — `Weather` — owns the current readings and all evolution rules. Its state is the multi-value **value objects** `Temperature`, `Wind` (Beaufort 0–12), `Sunshine` (0–100 intensity), and `Precipitation` (type ∈ {None, Rain, Snow, Hail} + intensity), each fully validated on construction so an invalid reading cannot exist (ADR-0003). The aggregate also holds the **active regime**: `Calm` (default), `Precipitation`, or `StrongWind`, plus a remaining-duration countdown.

Evolution is a single intent-revealing operation, `Advance(TimeSpan elapsed, IWeatherSampler sampler)`:
1. Decrement the active event's remaining time; if it hits zero, return the regime to `Calm`.
2. Compute the regime's **target** readings (Calm → defaults; Precipitation → cooler temp, slightly higher wind, chosen precipitation type; StrongWind → very high wind, lower sunshine, cooler temp).
3. Nudge each current reading a fraction of the way toward the target (mean-reversion) plus a small bounded random sample. `Calm` uses a small step so it hovers near defaults; event reversion uses a gradual step so readings ease back rather than snap.
4. Apply results through validated `SetX`/value-object operations, updating lifecycle state (`Modified` on real change, `Touched` on no-op).

Randomness is injected via an `IWeatherSampler` seam (default = `Random`-backed; tests supply a deterministic/seeded one), keeping `Advance` pure and unit-testable.

*Alternatives considered:* a separate stateless domain **service** holding all rules with the aggregate as an anemic bag — rejected because ADR-0003 mandates rich models that own their invariants and transitions. Event-sourcing the weather — rejected as overkill for an ephemeral single instance with no history requirement.

### Regimes as data-driven targets, not branchy special-casing

Precipitation and strong wind differ from calm only in their **target** readings, step sizes, and duration. Modeling each regime as a small target/step descriptor keeps `Advance` one code path and makes the two events (and future ones) additive rather than a growing `switch`. This is why `weather-simulation` (drift toward a target) and `weather-events` (which target + duration) can be separate specs sharing one mechanism.

### In-memory, thread-safe single-instance store

An `IWeatherStore` singleton holds the one `Weather` aggregate. Three actors touch it: the hosted simulation loop (writer), `GetWeatherQueryHandler` (reader), and the event command handlers (mutators). The store guards access with a lock (or an immutable snapshot swapped under a lock) so concurrent reads during a tick never observe a half-updated aggregate. No data store, matching the "operates on its own" ephemeral description.

### Hosted `BackgroundService` drives the clock via `TimeProvider`

A `WeatherSimulationService : BackgroundService` ticks on a fixed interval (e.g. a few seconds of wall-clock per simulation step) using `TimeProvider.CreateTimer`, calls `Weather.Advance(...)` under the store lock, and writes back. Injecting `TimeProvider` (not `Task.Delay`/`DateTime.Now`) makes the loop deterministic under `FakeTimeProvider` in tests, mirroring the queue module's approach. Event durations (~15 min precipitation, ~10 min strong wind) are expressed in simulation time so tests can fast-forward.

### CQRS feature slices + module-owned endpoints

Per ADR-0005/0006/0007, three features under `Weather.Features`:
- `GetWeather` — `GetWeatherQuery` → `WeatherConditionDto` (always returns a response).
- `StartPrecipitation` — `StartPrecipitationCommand(PrecipitationType)` → begins the event (no response body needed).
- `StartStrongWind` — `StartStrongWindCommand` → begins the event.

Endpoints live in the module's `Endpoints` namespace behind a `/weather` `MapGroup`: `GET /weather`, `POST /weather/precipitation`, `POST /weather/strong-wind`. The module exposes `AddWeatherModule()` (registers handlers, the store, the sampler, the hosted service, and the `IWeatherConditionProvider` implementation) and `MapWeatherEndpoints()`. The API host only calls these two.

### `Weather.Abstractions` carries the cross-module contract

`Weather.Abstractions` exposes `IWeatherConditionProvider` (returns the current `WeatherConditionDto`) plus the shared enums/DTOs (`PrecipitationType`, `WeatherConditionDto`). Other modules reference **only** `.Abstractions` (ADR-0004). The provider implementation in the module reads from `IWeatherStore`.

### Publish `WeatherUpdateIntegrationEvent` on every update

Every weather update — each simulation tick whose `Advance` yields a real change, and the moment a user triggers an event — publishes a `WeatherUpdateIntegrationEvent` carrying the new conditions (temperature, wind, sunshine, precipitation, active regime), so other services are notified. The event type lives in the central `FourDotnet.BoogaBooster.IntegrationMessages` library under `Events.Weather`, is suffixed `IntegrationEvent`, and carries a `[TopicName(...)]` attribute, per the `integration-messages` conventions. The module publishes through the injected publisher from `AddBoogaBoosterIntegrationMessages()` (Dapr pub/sub over RabbitMQ).

Mechanically, the hosted loop (and the event command handlers) obtain the post-update snapshot from the store and hand it to an `IWeatherUpdatePublisher` seam in the module; the default implementation maps the snapshot to `WeatherUpdateIntegrationEvent` and publishes it. A no-op publisher is registered until the `integration-messages` change is present, so the module still builds and runs standalone — but publishing is a required behavior of this change and the `integration-messages` change is its prerequisite. Ticks that produce no change (`Touched`, not `Modified`) do **not** publish, so idle calm weather is not a firehose of identical events.

*Alternative considered:* publishing directly from the domain aggregate — rejected because publishing is infrastructure; the aggregate signals change via lifecycle state and the loop/handlers own the publish.

## Risks / Trade-offs

- **Tuning the drift feels wrong (too jumpy or too flat)** → Keep step size, noise amplitude, and regime targets as named constants in one place; cover bounds and mean-reversion with tests so tuning stays safe.
- **Concurrency bug: query observes a partially advanced aggregate** → Single store lock (or immutable-snapshot swap); the aggregate is only mutated inside `Advance` under that lock, and readers take a consistent snapshot.
- **Real-time event durations make tests slow** → Durations run in simulation time driven by `TimeProvider`; tests advance a `FakeTimeProvider` instead of sleeping.
- **DDD lifecycle state is persistence-oriented but there's no store** → Apply ADR-0003 pragmatically: still derive from the shared base class and use `SetX`/value objects for invariants; lifecycle state is used as the change signal that also feeds the optional integration-event seam.
- **Over-splitting three specs for one mechanism** → Specs are split by observable behavior (baseline vs. events vs. API), not by class; they intentionally share the single `Advance` mechanism described here.
- **Depends on the not-yet-built `integration-messages` change for actual publishing** → The module talks to an `IWeatherUpdatePublisher` seam with a no-op default, so it builds and runs standalone; the real Dapr-backed publisher is swapped in once `integration-messages` lands. Flagged as a prerequisite in the proposal and tasks.
- **Event volume: publishing on every tick could flood consumers** → Publish only on real change (`Modified`), never on no-op (`Touched`) ticks; regime targets/step sizes keep calm weather mostly stable so idle publishing stays low.
