## Context

The ride runs as a guarded state machine in the `DigitalTwin` module (`Ride` aggregate, ADR-0003), advanced one fixed timestep at a time by `RideSimulationService`. Its safety verdict (`RideSafetyReason`) already gates the guarded transitions `Loading → Safe` and `Safe → Started`, and `Advance` already performs automatic condition-driven transitions (`Safe → Loading` when unsafe, `Stopping/EmergencyStop → Offloading` at rest, `Offloading → Idle` when empty). None of these consider the weather.

The `Weather` module (`weather-service`) owns the world weather and exposes `IWeatherConditionProvider` from `Weather.Abstractions`, including a `NiceWeather` indicator in `[0, 1]` where `0` is a severe storm or heavy precipitation and `1` is pleasant, moderate weather — the module's own normalized "how bad is it" signal, already folding wind (Beaufort) and precipitation into one number. The `Queue` module already consumes weather (`queue-depend-on-weather`) to scale arrivals, holding the latest `NiceWeather` in a thread-safe weather-influence state.

The frontend `security-panel` (top-right of the ride dashboard) shows load and security rows bound to ride telemetry, conveying safe/unsafe by icon **and** text, passing AXE / WCAG AA.

This change makes the weather a ride-safety interlock across all three: the ride assesses and reacts to it, the queue drains for it, and the panel shows it.

## Goals / Non-Goals

**Goals:**

- Grade the current weather into three ride-safety levels (Clear / Caution / Unsafe) from `NiceWeather`, with configurable thresholds.
- Block loading and force a *controlled* closure of the ride when the weather is Unsafe, reusing the existing state machine and its automatic transitions rather than inventing new lifecycle states.
- Drain a ride's waiting groups when the weather becomes Unsafe.
- Show the graded level in the security panel, driven by the authoritative server assessment, meeting WCAG AA.

**Non-Goals:**

- No new NuGet/npm dependencies; no new persistent state.
- No change to how the Weather module computes `NiceWeather`, or to the weather event contract.
- No new ride lifecycle state — weather closure is expressed through the existing `Stopping`/`Offloading`/`Idle` flow.
- No physics changes — a weather stop is a controlled ramp-down (cut power, apply brakes), identical to an operator `Stopping`, **not** an emergency stop.

## Decisions

### Decision: Derive the safety level from `NiceWeather`, not raw wind

The physics docs define no wind-speed safety limit, and the Weather module already integrates wind and precipitation severity into `NiceWeather` (`0` = storm/heavy precipitation). Grading off that single, normalized signal keeps one source of truth and avoids re-deriving severity from raw Beaufort in two modules.

- **Bands** (configurable, defaults): `Unsafe` when `NiceWeather < 0.25`; `Caution` when `0.25 ≤ NiceWeather < 0.5`; `Clear` when `NiceWeather ≥ 0.5`. Defaults line up with the frontend's existing `nicenessLabel` boundaries ("Severe/Poor" → closed, "Poor/Fair" → caution).
- **Alternative considered**: a wind-Beaufort threshold (e.g. close at gale force). Rejected as a second, competing severity model the two modules would have to keep in sync; `NiceWeather` already reflects strong wind.
- **Hysteresis**: closure uses `<` and reopening requires climbing back to the Clear band, giving a natural dead-band between the Unsafe (`<0.25`) and Clear (`≥0.5`) thresholds so the ride does not flap open/closed around a single boundary.

### Decision: Weather safety is an *input* to the `Ride` aggregate, assessed in the Application layer

The `Ride` aggregate must not reach out to the Weather module (it is a pure domain model). Instead a thin **`IWeatherSafetyAssessor`** (Application layer) reads `IWeatherConditionProvider` and maps conditions → `WeatherSafetyLevel` using injected `WeatherSafetyOptions`. `RideSimulationService` calls the assessor each tick and pushes the result into the ride via `ride.SetWeatherSafety(level)` **before** `ride.Advance(dt)`. The ride stores the level and factors it into its verdict and transitions.

- **Alternative considered**: the ride subscribes to `WeatherUpdateIntegrationEvent`. Rejected — the assessment is naturally read each tick in-process (the modular monolith hosts both modules), and keeping the domain model free of messaging concerns is cleaner. The event path is already used by the Queue.

### Decision: Express closure through the existing state machine, adding one `UnsafeWeather` safety reason and one automatic transition set

- `RideSafetyReason` gains `UnsafeWeather`. `Ride.EvaluateSafety()` returns it (with suitable priority) when the stored level is `Unsafe`. Because `Loading → Safe` and `Safe → Started` already guard on `IsSafe`, they are blocked for free.
- `Idle → Loading` currently has **no** guard; it gains one so the ride "cannot be loaded" while the weather is Unsafe.
- `Advance` gains a weather-closure step that runs before the existing automatic transitions:
  - `Started` → `Stopping` (controlled stop: cut power, engage brakes — the existing `EnterStopping` side-effect). The ride then coasts to rest and the existing `Stopping → Offloading → Idle` autos finish the job.
  - `Loading` or `Safe` (at rest, possibly loaded) → `Offloading` (release restraints so passengers disembark) → `Idle` via the existing empty-check.
  - `Idle` stays `Idle`; `Stopping`/`EmergencyStop`/`Offloading` are left to finish.
- These are **automatic** transitions, so — like the existing ones — they are excluded from `AvailableTransitions` and never surface as operator buttons.

### Decision: The Queue owns queue evacuation, reacting to the same weather signal

The Queue already receives weather updates and holds the latest `NiceWeather` (`queue-depend-on-weather`). This change extends that handling: when an incoming update is Unsafe (`NiceWeather` below the configured closure threshold), the Queue drains every ride's waiting groups (a new `RideQueue.Evacuate()` / clear-all operation). This keeps the queue's data owned by the Queue module and avoids a cross-module command from DigitalTwin.

- **Threshold consistency**: both modules key off `NiceWeather` and a closure threshold with the same default (`0.25`). Each is configurable independently (`QueueModuleOptions` / `WeatherSafetyOptions`); the design documents that they represent the same policy and should agree.
- **Alternative considered**: DigitalTwin calls `IRideQueueService.EvacuateAsync(rideId)` when the ride closes. Rejected — it couples the ride's internal state transition to queue mutation and duplicates the "unsafe" decision; reacting to the weather signal each already consumes is simpler and keeps ownership clean.

### Decision: The panel reads the authoritative server level from telemetry

`RideTelemetry` gains a `WeatherSafety` (`WeatherSafetyLevel`) field so the security panel shows exactly the level that drove the ride's behaviour, rather than the panel re-deriving a band from the weather feed client-side (which could disagree with the server's closure decision). The panel renders a third row with a distinct icon and worded state per level.

## Risks / Trade-offs

- **Two thresholds could drift** (Queue vs DigitalTwin) → Both default to `0.25` on `NiceWeather` and are documented as one policy; tests assert closure and evacuation trigger on the same band. A future refactor could hoist the policy into `Shared/Core`.
- **Flapping around the threshold** as `NiceWeather` drifts → the Clear/Unsafe dead-band (0.25 vs 0.5) plus the weather module's own mean-reverting, small-per-tick drift prevent rapid open/close cycling.
- **A ride mid-closure when weather recovers** → closure transitions, once begun (`Stopping`/`Offloading`), run to completion via the existing autos regardless of a weather rebound; the ride simply becomes loadable again once back at `Idle` in Clear/Caution weather. No special-casing needed.
- **Caution is advisory only** → by design it does not stop the ride; it only warns, so an operator keeps agency until conditions are genuinely unsafe.

## Migration Plan

Additive across the board — new enum members, a new telemetry field (defaulting to `Clear`), a new options block with safe defaults, and a new panel row. No data migration. Rollback is reverting the change; the state machine and weather/queue modules function without it. Ships behind the existing modular-monolith wiring (`AddDigitalTwinModule`, `AddQueueModule`) with no new external resources.

## Open Questions

- Should `Caution` also *slow* a running ride (e.g. cap engine power) rather than only warn? Deferred — out of scope; this change keeps Caution advisory.
- Final default thresholds (`0.25` / `0.5`) are best-guess and configurable; they may want tuning once the weather simulation's `NiceWeather` distribution is observed end-to-end.
