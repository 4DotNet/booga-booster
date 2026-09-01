## Why

The operator's "Apply brakes" button and the `Stopping` stage both claim to "apply
the brakes", but the twin has **no brake actuator** — `Ride.BrakeEngines` only
_cuts drive power_ ("there is no separate brake-torque actuator", Ride.cs:271) and
then lets the ride coast down under its ordinary friction. With the mill's very
large rotating inertia that coast takes far too long, so a "stopping" ride keeps
turning for many seconds and the operator's brake press feels inert. This change
makes braking real: a commanded brake that dramatically increases drag and brings
the ride to rest within a couple of seconds, both on the operator's button and
automatically when the ride enters `Stopping`.

## What Changes

- **BREAKING** — the engine brake becomes a **toggle** (on/off) instead of the
  current one-shot power-cut. The operator's "Apply brakes" button holds its
  engaged/released state; engaging it cuts drive power to zero **and** applies the
  brake, releasing it lets the ride be driven again.
- The digital-twin domain gains a **brake-engaged** state on the drive (mill + all
  hubs), changed through intent-revealing domain operations (ADR-0003). While
  engaged, a large **brake torque** is added to the rotational-drag model so the
  mill and hubs decelerate hard and settle to a complete stop in a couple of
  seconds; while released the ride runs with its normal friction.
- The brake torque magnitudes for the mill and the hubs are added to
  `RideParameters.cs` as new tuning constants, sized to give the "stop within a
  couple of seconds" feel.
- Entering `Stopping` (and `Emergency Stop`) automatically cuts power to zero and
  **engages the brake**, so the controlled/emergency ramp-down is fast; the brake
  is released again once the ride is at rest (entering `Offloading`) and when a new
  run begins (entering `Started`).
- Telemetry reports whether the brake is currently engaged, so the UI can render
  the toggle state truthfully.
- The Angular controls turn "Apply brakes" into a real toggle bound to the
  reported brake state, and **disable the mill/hub power sliders while the brake is
  engaged**, re-enabling them when it is released.

## Capabilities

### New Capabilities
- `engine-braking`: Commanding an engine brake (a toggle that cuts drive power and
  applies a strong decelerating brake torque to the mill and hubs), the fast
  coast-to-rest dynamics that follow, the automatic brake applied when the ride is
  stopping, and the reporting/UI gating (power sliders disabled while braking) that
  goes with it.

### Modified Capabilities
<!-- The ride-state-machine spec already describes "power is cut and brakes are applied"
     for Stopping / Emergency Stop; this change implements that behaviour physically
     without changing its stated requirements. No previously published (archived) specs
     exist in openspec/specs/, so there are no requirement-level modifications to record. -->

## Impact

- **Domain** (`DigitalTwin/…/Domain`): new `MillBrakeTorque` / `HubBrakeTorque`
  constants in `RideParameters`; brake-engaged state + `EngageBrakes()` /
  `ReleaseBrakes()` on `GreatMill`, threaded into `Hub.AdvancePhysics`; the brake
  torque folded into the drag passed to `RotationalDynamics.Integrate`; `Ride`
  gains `SetEngineBrakes(bool)` (replacing the one-shot `BrakeEngines()`) and its
  `EnterStopping`/`EnterEmergencyStop`/`EnterStarted`/`EnterOffloading` entry
  side-effects engage/release the brake.
- **Application / API**: `IRideStore.BrakeEngines()` → `SetEngineBrakes(bool)`;
  the `BrakeEngines` command carries an `Engaged` flag; `POST /ride/engine-brake`
  takes a `SetEngineBrakeRequest(bool Engaged)` body.
- **Abstractions**: `RideTelemetry` gains a `BrakesEngaged` flag.
- **Frontend** (`FourDotnet.BoogaBooster.App`): `sse-ride-telemetry-source` posts
  the engaged flag; `ride.models` `BrakeEnginesCommand` + wire DTO + `mapRideTelemetry`
  carry `brakesEngaged`; `ride-state.service` exposes the brake state and a
  toggle; `operation-controls` makes "Apply brakes" a toggle and disables the power
  sliders while braking.
- **Tests**: domain tests for the brake drag (fast stop, released ride runs
  normally), auto-brake on stopping, telemetry reporting; store/endpoint coverage
  for the toggle; frontend specs for command dispatch, telemetry mapping, and
  slider gating.
