## Why

The mill and hubs can be driven past the rotation speeds their arms are built to survive. At those speeds the structure and its passengers face damaging — potentially injurious — stresses, yet nothing today detects the over-speed or intervenes. The ride needs an over-speed protection layer that warns before the limit and trips a protective stop when the limit is crossed.

## What Changes

- Add per-component rotation-speed limits: the **mill** warns above **15 rpm** and is unsafe above **18 rpm**; each **hub** (shorter arm, higher tolerance) warns above **26 rpm** and is unsafe above **32 rpm**.
- Introduce a three-level **stress** reading — `safe` / `warning` / `failure` — derived from the worst of the mill's and every hub's rotation speed against its limits.
- When any component reaches its unsafe limit while the ride is running, the ride **automatically enters safety mode**: it stops and brakes are applied to the mill and the hubs (power cut, brakes engaged) with no operator action.
- The **rotation speed panel** flags the mill value and each hub value that is at or over its warn limit.
- The **Load & security panel** gains a new **Stress** row that reads `safe`, `warning`, or `failure`.

## Capabilities

### New Capabilities
- `overspeed-protection`: rotation-speed warn/unsafe limits for the mill and hubs, the roll-up stress reading, the automatic safety-mode trip, and the operator-facing stress/warn indications.

### Modified Capabilities
<!-- No existing captured specs (openspec/specs is empty); this behavior is introduced wholesale as a new capability. -->

## Impact

- **Backend (`DigitalTwin` module)**
  - `Domain/RideParameters.cs` — new mill/hub warn and safety rpm constants.
  - `Domain/GreatMill.cs`, `Domain/Hub.cs` — stress classification per component and a ride-wide roll-up.
  - `Domain/Ride.cs` — automatic over-speed trip into `EmergencyStop` from the running physics step.
  - `Abstractions/MillTelemetry.cs`, `Abstractions/HubTelemetry.cs`, new `Abstractions/StressLevel.cs` — carry the stress reading on the wire.
- **Frontend (`FourDotnet.BoogaBooster.App`)**
  - `ride-dashboard/models/ride.models.ts` — `StressLevel` type, mirrored rpm-limit constants, stress-derivation helpers, mapping the new telemetry field.
  - `panels/speed-panel` — per-value warn/failure indication.
  - `panels/security-panel` — the new Stress row.
  - `ride-dashboard.html` / state service — wire the stress reading into the security panel.
- **Tests** — `DigitalTwin.Tests` (stress classification + automatic trip) and the Angular panel/model specs.
- No API contract removals; telemetry records gain fields (additive).
