## Context

The digital twin (`DigitalTwin` module) models the ride as a rich DDD aggregate: `Ride` (aggregate root) owns a `GreatMill`, which owns four `Hub`s, each owning four `Gondola`s. A guarded state machine in `Ride` governs the lifecycle (`Idle → Loading → Safe → Started → Stopping/EmergencyStop → Offloading`). `Ride.Advance(dt)` steps natural passenger behaviour, automatic condition-driven transitions, and — while running — the physics. Rotation speed is already derived (`RotationalDynamics.ToRpm`) and surfaced on `MillTelemetry.Rpm` / `HubTelemetry.Rpm`.

There is no over-speed detection today. The mill can reach ~24 rpm and a hub ~48 rpm at full power, so the requested 18 rpm / 32 rpm safety limits are physically reachable. Safety interlocks already exist as a pattern: `EvaluateSafety()` returns a `RideSafetyReason`, and `Advance` already performs one automatic transition (`Safe → Loading` when a loaded ride stops being safe). Emergency stop entry (`EnterEmergencyStop`) already cuts mill+hub power and engages the gondola brakes — exactly the "stop and apply brakes" behaviour safety mode needs.

The Angular dashboard's telemetry rail renders a `SpeedPanel` (mill + hub rpm) and a `SecurityPanel` ("Load & security"). The frontend already mirrors backend constants that must "stay in sync" (`MILL_MAX_POWER_WATTS`, `MAX_SAFE_ECCENTRICITY`) and derives some roll-ups client-side.

Per ADR-0003, domain changes must be rich domain models with private state and intent-revealing members; per the unit-testing guideline, backend tests use xUnit v3 + Moq/Bogus (no FluentAssertions) and cover ≥80% of the module.

## Goals / Non-Goals

**Goals:**
- Classify mill and hub rotation speed against warn/safety limits and roll them into one `safe`/`warning`/`failure` stress reading.
- Automatically stop the ride and apply mill+hub brakes when any component crosses its safety limit while running — no operator action.
- Surface per-component stress on telemetry; warn per value in the speed panel and show a Stress row in the Load & security panel.

**Non-Goals:**
- A new dedicated brake-torque actuator. This model already defines braking as cutting drive power + engaging gondola brakes (as `EnterEmergencyStop` does); safety mode reuses that.
- A new `RideState` value (e.g. a distinct `Safety`). Safety mode is the existing `EmergencyStop` entered automatically; adding a state would ripple through the frontend enum index contract for no behavioural gain.
- Blocking an operator from starting a stationary ride based on stress (stress is a running/over-speed concern; at rest all speeds are 0 = `safe`).
- Changing the imbalance/overload/restraint interlocks.

## Decisions

### 1. Model stress as a three-level enum in Abstractions
Add `StressLevel { Safe, Warning, Failure }` to `DigitalTwin.Abstractions` (public wire contract, ordered so a numeric `Max` gives "worst wins"). `Safe` is index 0 so a default/at-rest value is safe.
- *Alternative — booleans (`IsOverSpeed`/`IsWarnSpeed`):* rejected; two booleans can express the impossible "failure-but-not-warning" and read worse at call sites than one ordered enum.

### 2. Thresholds live in `RideParameters`
Add `MillWarnRpm = 15`, `MillSafetyRpm = 18`, `HubWarnRpm = 26`, `HubSafetyRpm = 32`. `RideParameters` is already the single home for physical constants and limits.

### 3. Classification is computed on the domain models from |rpm|
Add a small pure classifier (warn/safety limits → `StressLevel`) used by both `GreatMill` (mill limits, from its own rpm) and `Hub` (hub limits, from its own rpm). Compare on `Math.Abs(rpm)` because the reverse-ride-direction work allows negative rpm. `GreatMill` exposes a ride-wide roll-up (`StressLevel Stress`) = `Max(mill, max over hubs)`. Reuse `RotationalDynamics.ToRpm(omega)` so the trip decision and the displayed rpm agree.
- *Alternative — classify in the telemetry mapper only:* rejected; the automatic trip in `Advance` needs the classification in the domain, so it belongs on the domain models with telemetry projecting it.

### 4. Automatic trip in `Ride.Advance`, reusing emergency-stop entry
In the physics branch of `Advance`, after `AdvancePhysics`, if `_state == Started` and `_mill.Stress == Failure`, run the emergency-stop entry (cut mill+hub power, engage gondola brakes) and set `_state = EmergencyStop`. This mirrors the existing automatic `Safe → Loading` demotion and keeps every "stop + brake" side-effect in one place (`EnterEmergencyStop`). Once tripped, cut power makes the ride coast down, stress falls back through `warning`→`safe`, and the existing `EmergencyStop → Offloading` settle-at-rest logic proceeds unchanged.
- *Trigger only from `Started`:* `Stopping`/`EmergencyStop` already have power cut and are decelerating, so re-tripping them is unnecessary; `Started` is the only powered running state.

### 5. Telemetry carries per-component stress; frontend rolls up
Add `StressLevel Stress` to `MillTelemetry` (mill's own) and `HubTelemetry` (hub's own). The Load & security panel's ride-wide reading is the worst of the mill's and the hubs' — computed on the frontend, consistent with how the app already derives roll-ups. Frontend adds a `StressLevel = 'safe' | 'warning' | 'failure'` type, mirrors the four rpm limits as constants with "must stay in sync" comments (matching the existing `MILL_MAX_POWER_WATTS` pattern), and maps the new wire field.
- *Alternative — emit only the ride-wide roll-up:* rejected; the speed panel needs per-value warnings, so per-component stress must be on the wire anyway, and the roll-up is then trivial to derive.

### 6. Presentation
`SpeedPanel` flags the mill value and each hub value by `warning`/`failure` using a `data-state` attribute plus an icon/text badge (never colour alone — WCAG AA). `SecurityPanel` gains a `stress` input and a third row "Stress: <level>" with an icon, following its existing icon+text row pattern. `ride-dashboard.html` wires the ride-wide stress from the state service into `SecurityPanel`; `SpeedPanel` derives per-value levels from the rpm it already receives.

## Risks / Trade-offs

- **Duplicated thresholds (backend + frontend).** → Accepted and explicitly documented with "must stay in sync" comments, exactly as the codebase already does for max power watts and eccentricity. Backend remains authoritative for the trip.
- **Reverse direction → negative rpm misclassified.** → Classify on `Math.Abs(rpm)` so limits are symmetric.
- **Momentary boundary flicker between `warning` and `failure` while coasting.** → Acceptable: the trip is one-way (it moves to `EmergencyStop` and cannot return to `Started` automatically), so a failure is latched into a full stop rather than oscillating.
- **New telemetry fields are additive.** → Existing consumers ignore unknown/defaulted fields; `Safe` (index 0) is the natural default, so a stale consumer degrades gracefully.
- **Frontend panels may be simulator- or feed-driven during the in-flight `connect-data-panels` work.** → Deriving per-value stress from whatever rpm the panel is handed keeps it correct under either source.

## Migration Plan

Additive, no data migration. Ship backend (params, classifier, telemetry fields, trip) and frontend (type, constants, panels) together so the new Stress row and warnings render. Rollback is reverting the change; the additive telemetry fields are safe to leave unread.

## Open Questions

- None blocking. The limits (15/18 mill, 26/32 hub) and the reuse of `EmergencyStop` as safety mode are taken as specified.
