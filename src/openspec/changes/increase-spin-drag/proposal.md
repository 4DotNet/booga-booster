## Why

When the drive power is cut, the rig coasts down under its friction and aerodynamic losses only — and today those losses are far too small for the rig's enormous rotating inertia. The mill in particular keeps creeping for well over a minute after the engines are off, which reads as unrealistic and makes the "stopping" phase of the ride drag on. The loss coefficients need to be raised — especially the mill's — so a de-powered rig settles to rest in a believable time.

## What Changes

- **Raise the mill's loss coefficients** (`MillCoulombFriction`, `MillViscousFriction`, `MillAeroDrag`) so an unpowered mill coasts to a complete stop in a realistic, bounded time instead of creeping for minutes. The mill gets the largest relative increase, matching where the problem is most visible.
- **Raise the hubs' loss coefficients** (`HubCoulombFriction`, `HubViscousFriction`, `HubAeroDrag`) by a smaller factor, so the hubs also settle promptly and the whole rig reaches `IsAtRest` without a long tail.
- **Bias the increase toward Coulomb and viscous friction** (the terms that dominate the low-speed tail — the actual "keeps spinning forever" symptom) with a more modest bump to aerodynamic drag, because aero drag (∝ ω²) is already the dominant loss at cruise and pushing it hard would drop the ride's terminal/cruise speed and felt G more than intended.
- **Add a coast-down duration property test** asserting the mill (empty and fully loaded) comes to rest within a target time window after power is cut — so the tuning is pinned by a test and can't silently regress.
- **Update the physics docs** (`docs/03-motor-power-and-torque.md` §3.3/§3.4 loss notes and `docs/appendix-parameters.md` loss-coefficient baseline) to reflect the retuned golden-scenario values and the target coast-down time.

No API, telemetry contract, or state-machine behaviour changes — this is a physics-tuning change to named constants plus a guarding test.

## Capabilities

### New Capabilities

- `ride-coast-down`: The rig's deceleration behaviour once drive power is cut — the friction and aerodynamic loss model that brings the mill and hubs to a complete, bounded-time rest, and the target coast-down window the tuned loss coefficients must satisfy (while keeping the powered terminal/cruise speed within its existing range).

### Modified Capabilities

<!-- None — openspec/specs/ has no published baseline; the rotational-dynamics loss model this retunes is documented in docs/02–03 and lives in RideParameters/RotationalDynamics, and is captured here as a new capability rather than a delta on a published spec. -->

## Impact

- **DigitalTwin module** (`src/DigitalTwin/FourDotnet.BoogaBooster.DigitalTwin/Domain/RideParameters.cs`): new values for the six mill/hub loss constants (`Mill*`/`Hub*` `CoulombFriction`, `ViscousFriction`, `AeroDrag`). No change to `RotationalDynamics` — the integrator and loss formula are correct; only the coefficients change.
- **Tests** (`src/Tests/FourDotnet.BoogaBooster.DigitalTwin.Tests/DrivenBodyTests.cs`): a new coast-down duration test; the existing energy-monotonicity and terminal/over-speed-cap tests are re-checked against the retuned values and must still pass.
- **Docs** (`docs/03-motor-power-and-torque.md`, `docs/appendix-parameters.md`): loss-coefficient baseline and coast-down/terminal sanity-check figures updated to the retuned values.
- **Frontend / telemetry**: none. RPM telemetry simply reports the faster settle; no contract or component change.
- **Dependencies**: no new NuGet or npm packages.
