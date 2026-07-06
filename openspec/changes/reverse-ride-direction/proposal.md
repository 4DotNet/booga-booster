## Why

The operation-controls panel already shows Forward/Reverse toggles for the central mill and the hubs, but pressing them does nothing to the ride: the toggle state is echoed locally in the browser and immediately reverted by the next telemetry frame. The reason is that reversal was never built end-to-end — the digital-twin domain has no notion of drive direction at all. The motor torque is always positive, so the mill and hubs can only ever spin one way. This change makes the toggles real: the operator can command either motor group to run the other way, and the twin's physics respond faithfully.

## What Changes

- The digital-twin domain gains a **commanded motor direction** (Forward / Reverse) on the central mill and on each hub, defaulting to Forward, changed through intent-revealing domain operations (ADR-0003).
- The rotational-drive core applies the commanded direction as the **sign of the drive torque**. Reversing while a body is already spinning makes the drive oppose the current motion, so the body decelerates through a standstill and then accelerates the other way — friction and inertia produce the "slow down, stop, spin up in reverse" behaviour for free.
- Two new command endpoints (`POST /ride/main-direction`, `POST /ride/hub-direction`) let the operator set each motor group's direction, mirroring the existing power endpoints.
- Mill and hub telemetry report the **commanded direction**, and rotation speed (`Rpm`) becomes truthfully **signed** (negative while running reversed) so consumers can render the actual turn direction, including the transient during a reversal.
- The Angular controls dispatch the direction commands to the new endpoints (replacing the local-only stub), telemetry mapping reads the reported direction instead of hard-coding `'forward'`, and the 3D visualization turns from the signed speed so it shows the true slow-down-then-reverse.

## Capabilities

### New Capabilities
- `motor-direction-control`: Commanding and reporting the rotation direction of the mill and hub motor groups, and the reversing drive dynamics (decelerate through rest, then accelerate the opposite way) that follow a direction change while spinning.

### Modified Capabilities
<!-- No previously published (archived) specs exist in openspec/specs/, so there are no requirement-level modifications to record here. -->

## Impact

- **Domain** (`DigitalTwin/…/Domain`): new `MotorDirection` type; direction state + `SetDirection` operations on `GreatMill` and `Hub`; signed drive in `RotationalDynamics.MotorTorque`/the bodies' `AdvancePhysics`; direction surfaced through `Ride`.
- **Application / API**: `IRideStore`/`RideStore` gain direction setters; new `SetMainEngineDirection` / `SetHubEngineDirection` CQRS features; two new POST endpoints in `DigitalTwinEndpoints`.
- **Abstractions**: `MillTelemetry` and `HubTelemetry` gain a `Direction` field; `Rpm` semantics become signed.
- **Frontend** (`FourDotnet.BoogaBooster.App`): `sse-ride-telemetry-source` posts the direction commands; `ride.models` stream DTOs + `mapRideTelemetry` carry direction; `ride-visualization` turns from signed speed; gauges audited for signed rpm.
- **Tests**: new domain tests for reversing dynamics and direction state; endpoint/store coverage for the new setters; frontend specs for command dispatch, telemetry mapping, and visualization direction.
