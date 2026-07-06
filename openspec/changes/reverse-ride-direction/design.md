## Context

The ride is a modular-monolith digital twin. The physics live in
`DigitalTwin/…/Domain`: `RotationalDynamics` is a pure drive/friction/integrator
core; `GreatMill` and each `Hub` are rich domain models (ADR-0003) that call it
each simulation step. Operator commands flow browser → `POST /ride/*` →
CQRS command handler → `IRideStore` → `Ride` aggregate. Telemetry flows the other
way as `RideTelemetry`/`MillTelemetry`/`HubTelemetry` DTOs, streamed over SSE to
the Angular dashboard.

Today there is **no direction concept anywhere in the stack**. `EnginePower` is a
0–100 % throttle with no sign; `MotorTorque` returns a non-negative magnitude;
the bodies can only spin one way. The frontend already has Forward/Reverse
toggles, a `MotorDirection` type, and a visualization that can turn either way —
but `sse-ride-telemetry-source` makes **no HTTP call** for a direction command
(it only echoes locally) and `mapRideTelemetry` hard-codes `direction: 'forward'`
because "the backend does not model a reverse direction." This change closes that
gap end-to-end.

Two facts make the backend change small:
- `RotationalDynamics.Integrate` already handles a **signed** drive torque and a
  signed `omega`: static friction is applied for both signs of drive at rest
  (`driveTorque > coulomb` / `< -coulomb`), and while moving `netTorque =
  driveTorque - sign(omega)·loss`. So a negative drive against a positive `omega`
  naturally decelerates the body through zero and accelerates it negative.
- `MotorTorque` already divides by `Math.Abs(omega)`, so it yields a correct
  torque **magnitude** regardless of spin sign.

The only missing piece is a **sign** on the drive torque, plus plumbing to
command and report it.

## Goals / Non-Goals

**Goals:**
- Add a commanded Forward/Reverse direction to the mill and to the hub group,
  defaulting to Forward, set through intent-revealing domain operations.
- Make the drive torque signed by direction so reversing a spinning body slows it
  through rest and then accelerates it the other way, purely from the existing
  friction/inertia model.
- Command direction from the UI through real endpoints and report it (plus a
  signed speed) in telemetry, so the toggles and the 3D view become truthful.

**Non-Goals:**
- No new physics beyond the sign of the drive (no braking-torque actuator, no
  per-hub independent direction — all four hubs share one commanded direction,
  matching the existing single hub-power command).
- No change to power semantics, safety interlocks, or the lifecycle state machine.
- Reversal is allowed whenever power commands are (i.e. while `Started`); this
  change does not add new gating for when a direction may be changed.

## Decisions

### D1 — Model direction as its own concept, not a signed power

Introduce a `MotorDirection` type in the domain (Forward / Reverse) with a
`+1 / -1` sign, and store it as encapsulated state on `GreatMill` and `Hub`
alongside `EnginePower`, changed via `SetDirection(MotorDirection)` using the
base-class `ApplyChange` (so it participates in lifecycle state and no-op
detection per ADR-0003).

*Alternative considered:* fold a sign into `EnginePower` (range −100…100).
Rejected — power (a 0–100 % throttle) and direction are distinct operator inputs
with distinct UI controls and distinct commands; conflating them would force the
power slider and the direction toggle through one value object and complicate the
"direction independent of power" invariant.

Representation: a small enum `MotorDirection { Forward, Reverse }` plus a `Sign()`
helper (extension or static map) returning `+1d` / `-1d`. An enum serializes
cleanly to telemetry and parses cleanly from the endpoint request, matching how
`SeatPosition`/`GondolaBrakeState`/`RideState` are already handled.

### D2 — Apply direction as the sign of the drive torque in `AdvancePhysics`

`MotorTorque` keeps returning a non-negative magnitude. Each body multiplies it
by its direction's sign before handing it to `Integrate`:

```
var drive = _direction.Sign() * RotationalDynamics.MotorTorque(
    _power.Fraction, _omega, stallTorque, maxPowerWatts);
```

`Integrate` is unchanged — it already copes with a signed drive. This is the
smallest correct change and keeps the reversing behaviour (decelerate → rest →
accelerate opposite) as an emergent property of the existing integrator rather
than special-cased logic.

*Alternative considered:* teach `MotorTorque` to take a signed throttle.
Rejected — it would have to re-derive the sign internally and re-clamp against a
signed stall torque; multiplying the magnitude at the call site is clearer and
leaves the pure core's contract (non-negative magnitude) intact.

### D3 — Telemetry: add a commanded `Direction`, keep `Rpm` signed

Add `Direction` (the commanded direction) to `MillTelemetry` and `HubTelemetry`.
Leave `Rpm` as the **signed** value `ToRpm(omega)` already produces — it is
non-negative today only because nothing spins backwards yet. Two distinct
consumers need two distinct things:
- the **toggle** must show the *commanded* direction, even at rest where speed is
  zero and its sign says nothing → needs the explicit `Direction` field;
- the **visualization** must show the *actual* instantaneous turn, including the
  reversal transient where the body still coasts the old way → needs the *signed
  speed*.

Frontend consequences:
- `mapRideTelemetry` reads `direction` from the new field instead of the
  `'forward'` literal; the stream DTOs (`RideTelemetryMillStreamDto`,
  `RideTelemetryHubStreamDto`) gain a `direction` member (numeric-or-name, parsed
  like the other wire enums).
- `ride-visualization` turns each body from the **signed rpm alone**;
  `angularVelocity(rpm, direction)` must stop *also* multiplying by the commanded
  direction, or it would double-count and cancel the reversal. The commanded
  direction inputs remain for labelling but no longer scale rotation.
- Speed gauges display magnitude (`Math.abs(rpm)`) with the direction shown
  separately; audit `status-summary`/`ride-state.service` derivations for any
  assumption that rpm is non-negative.

### D4 — Command surface mirrors power exactly

Add `SetMainEngineDirection` / `SetHubEngineDirection` to `Ride`, then to
`IRideStore`/`RideStore`, then a CQRS command+handler pair per group under
`Features/`, then `POST /ride/main-direction` and `POST /ride/hub-direction` in
`DigitalTwinEndpoints` with a `SetDirectionRequest(string Direction)` parsed
case-insensitively (rejecting unknown values with 400), exactly like the existing
power/brake endpoints. The Angular `sse-ride-telemetry-source` replaces its
local-only `set-mill-direction` / `set-hub-direction` stubs with a real POST plus
the same optimistic echo it already uses for power.

## Risks / Trade-offs

- **Double-applied direction in the visualization** → the viz currently does
  `rpmToRadPerSec(rpm) · dirSign`. With `Rpm` now signed, leaving that multiply in
  place would cancel reversal (negative rpm × reverse = forward). Mitigation: make
  the viz drive purely from signed rpm and update `ride-visualization.spec.ts` to
  lock in that a negative rpm turns the body backwards.
- **Consumers assuming non-negative rpm** → gauges/means could render oddly with a
  negative value. Mitigation: audit the frontend rpm consumers and display
  magnitude + direction; hub signs always agree (one shared hub direction) so the
  hub-speed mean stays coherent.
- **Frontend/backend enum drift** → the wire `Direction` must map to the
  `'forward'|'reverse'` union. Mitigation: reuse the existing numeric-or-name
  `enumIndexOf` pattern already used for brake/seat/restraint enums.
- **Reversal step granularity** → at large `dt` a body could cross zero within one
  step; semi-implicit Euler still yields a continuous, monotone-through-zero
  result because the drive keeps its sign, so no special clamp is needed (the
  existing coast-to-rest clamp only fires when `driveTorque == 0`). Covered by a
  turnaround test.

## Migration Plan

Additive throughout — no data migration (the ride is ephemeral, in-memory) and no
breaking API change (new endpoints, new optional telemetry field defaulting to
Forward). Backend and frontend land together; if only the backend ships, the UI
still functions (it just keeps showing Forward). Rollback is reverting the change;
no persisted state is affected.

## Open Questions

- Should the speed gauges show a signed value or magnitude-plus-arrow? Assumed
  magnitude-plus-direction to match the existing toggle idiom; easily revisited in
  the frontend task without touching the contract.
