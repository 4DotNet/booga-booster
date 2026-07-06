## Context

The ride is a modular-monolith digital twin. The physics live in
`DigitalTwin/…/Domain`: `RotationalDynamics` is a pure drive/friction/integrator
core; `GreatMill` and each `Hub` are rich domain models (ADR-0003) that call it
each simulation step. Operator commands flow browser → `POST /ride/*` → CQRS
command handler → `IRideStore` → `Ride` aggregate; telemetry flows back as
`RideTelemetry` over SSE to the Angular dashboard.

Braking exists today only as a **power cut**. `Ride.BrakeEngines()` (Ride.cs:275)
calls `_mill.CutAllPower()` and nothing else — its own doc-comment says "there is
no separate brake-torque actuator". `EnterStopping`/`EnterEmergencyStop`
(Ride.cs:451-462) likewise `CutAllPower()` + `EngageAllGondolaBrakes()` (the
gondola _yaw_ brakes, unrelated to the spin drive) and then rely on ordinary
friction to coast the ride down. Because the mill's inertia is huge
(~1.65e5–3e5 kg·m²) and its losses are tuned for a "believable coast", a de-powered
ride keeps turning for a long time — so the operator's "Apply brakes" button and
the `Stopping` stage both feel inert.

The drag model is the lever. `RotationalDynamics.Integrate`
(RotationalDynamics.cs:33) computes `lossMagnitude = coulomb + viscous·|ω| +
aero·ω²`, applies it opposing motion (`netTorque = drive − sign(ω)·loss`), holds a
body at rest with static friction, and clamps a coasting body cleanly to zero when
it would cross through. A large **Coulomb-style** brake term added to that loss
produces a near-constant decelerating torque — the fastest, most predictable way to
bring a spinning body of any speed to a hard stop — and the integrator's existing
"don't reverse while coasting" clamp lands it exactly on zero.

The command/telemetry plumbing for this feature already exists end-to-end (endpoint
`POST /ride/engine-brake` → `BrakeEnginesCommand` → `IRideStore.BrakeEngines()` →
`Ride.BrakeEngines()`; frontend "Apply brakes" button → `ride-state` →
`sse-ride-telemetry-source`). This change evolves that path from a one-shot cut
into a stateful toggle and adds the missing brake torque.

## Goals / Non-Goals

**Goals:**
- Add a single ride-wide **brake-engaged** state to the drive, set through
  intent-revealing domain operations, that cuts drive power and applies a strong
  brake torque to the mill and hubs.
- Bring a running ride to a complete rest within a couple of simulated seconds when
  the brake is engaged, using new `RideParameters` brake-torque constants.
- Apply the brake automatically on `Stopping`/`Emergency Stop` and release it on
  `Offloading`/`Started`.
- Make the operator "Apply brakes" button a real toggle bound to reported state,
  and disable the power sliders while braking.

**Non-Goals:**
- No per-motor independent brake (mill and all hubs share one brake, matching the
  single hub-power/hub-direction commands).
- No change to the lifecycle state machine's transition table or guards — the
  manual brake toggle leaves the lifecycle state unchanged, exactly as the current
  `BrakeEngines()` does.
- No change to the gondola yaw brakes (`SetGondolaBrake`, `EngageAllGondolaBrakes`)
  — a separate, unrelated mechanism.
- No proportional/anti-lock brake curve; a constant brake torque is enough for the
  required feel.

## Decisions

### D1 — Model the brake as one ride-wide state on `GreatMill`, threaded to the hubs

The brake is a single operator toggle affecting the whole drive, so it lives as one
encapsulated `bool` on `GreatMill` (`_brakesEngaged`, default `false`) with a
`BrakesEngaged` getter and `EngageBrakes()` / `ReleaseBrakes()` operations.
`EngageBrakes()` also calls `CutAllPower()` (power → 0 on mill + hubs) so "engaged"
always implies "no drive". The mill already threads its angle and speed into
`Hub.AdvancePhysics`, so it threads the brake flag the same way rather than
duplicating state on each hub.

*Alternative considered:* a `bool` on `GreatMill` **and** on each `Hub`, set via a
fan-out like `SetAllHubDirection`. Rejected — the hubs never brake independently, so
a second copy of the state is redundant and risks drift; passing the flag down each
step (as angle/ω already are) keeps a single source of truth.

*Alternative considered:* a `BrakeState { Released, Engaged }` enum mirroring
`GondolaBrakeState`. Rejected as over-modelling — this is a binary drive-wide toggle
with no third state; a `bool` reads clearly and telemetry carries it as `bool`.

### D2 — Apply the brake as an added Coulomb term in the drag, in `AdvancePhysics`

`RotationalDynamics.Integrate` stays a pure function with an unchanged signature.
Each body folds the brake torque into the **Coulomb friction** it passes in:

```
var coulomb = RideParameters.MillCoulombFriction
    + (brakesEngaged ? RideParameters.MillBrakeTorque : 0d);
```

A Coulomb (speed-independent) term gives an almost constant decelerating torque, so
stop time ≈ `I·ω / τ_brake` regardless of drag curve shape, and the integrator's
static-friction branch then holds the body at rest (drive is zero while braking).
The existing "coasting can't reverse" clamp (RotationalDynamics.cs:74) guarantees
the body lands exactly on zero rather than jittering.

*Alternative considered:* add a dedicated `brakeTorque` parameter to `Integrate`.
Rejected — it would duplicate the Coulomb handling (static-hold at rest, sign
against motion) that the coulomb argument already implements; reusing that argument
is the smallest correct change and keeps the pure core's contract intact.

*Alternative considered:* scale up viscous/aero drag while braking. Rejected —
those are speed-dependent, so they fade to nothing at low speed and leave a slow
final crawl; a constant Coulomb brake is what produces a crisp, fast stop.

### D3 — Size the brake constants for a "couple of seconds" stop

Add `MillBrakeTorque` and `HubBrakeTorque` to `RideParameters` (SI, N·m), grouped
with the existing mill/hub motor-and-loss constants and documented as brake
actuators. Target stop time `t = I·ω_max / τ_brake ≈ 2 s`:
- Mill: `I ≈ 3e5` (loaded), `ω_max = 2.5` ⇒ `τ ≈ 3.75e5`; choose
  `MillBrakeTorque = 400_000` (well above `MillStallTorque = 60_000`, as a brake
  should be).
- Hub: sized from the hub inertia and `HubMaxAngularVelocity = 5` for a comparable
  ~2 s stop; choose `HubBrakeTorque = 40_000` (above `HubStallTorque = 8_000`).

These are tuning values; the domain tests assert the qualitative requirement (comes
to rest within a couple of seconds), so the exact figures can be trimmed against the
running twin without touching the contract.

### D4 — Evolve the existing command surface into a toggle (BREAKING)

Keep the `BrakeEngines` feature slice and the `POST /ride/engine-brake` route, but
carry an explicit desired state so the button is a true toggle:
- `Ride.BrakeEngines()` → `Ride.SetEngineBrakes(bool engaged)` calling
  `_mill.EngageBrakes()` / `_mill.ReleaseBrakes()`, then `MarkChanged()`.
- `IRideStore.BrakeEngines()` → `SetEngineBrakes(bool engaged)`.
- `BrakeEnginesCommand` gains `bool Engaged`; the handler passes it through.
- The endpoint takes a `SetEngineBrakeRequest(bool Engaged)` body (default `false`)
  and dispatches `new BrakeEnginesCommand(request.Engaged)`.

This is breaking for any caller relying on the no-body one-shot POST, but the only
caller is this repo's own frontend, updated in the same change.

### D5 — Auto-brake wiring on the lifecycle entry side-effects

`EnterStopping` and `EnterEmergencyStop` call `_mill.EngageBrakes()` (which already
cuts power) instead of a bare `CutAllPower()`; they keep engaging the gondola yaw
brakes as before. `EnterStarted` and `EnterOffloading` call `_mill.ReleaseBrakes()`
so a fresh run and an at-rest offloading ride both report the brake released. The
automatic `Stopping/EmergencyStop → Offloading` transition in `Advance`
(Ride.cs:377-382) runs `EnterOffloading`, so the release happens there too — the
release is centralised in the entry side-effect, not duplicated in `Advance`.

### D6 — Telemetry: one ride-wide `BrakesEngaged` flag on `RideTelemetry`

Because the brake is a single drive-wide toggle (not per-motor like `Direction`),
add `BrakesEngaged` (bool) to `RideTelemetry` rather than to `MillTelemetry`/
`HubTelemetry`. `Ride.ToTelemetry()` reads `_mill.BrakesEngaged`. Frontend
consequences:
- `mapRideTelemetry` reads a new `brakesEngaged` wire field into the UI model.
- `BrakeEnginesCommand` in `ride.models.ts` gains `engaged: boolean`;
  `sse-ride-telemetry-source.brakeEngines(engaged)` POSTs `{ engaged }` and keeps
  its optimistic echo (zeroing mill+hub power only when engaging).
- `ride-state.service` exposes a `brakesEngaged` signal and a toggle method.
- `operation-controls` binds "Apply brakes" as a toggle (`aria-pressed`) and adds
  `brakesEngaged()` to the condition that disables the power-slider form, so the
  sliders greys out while braking and returns when released.

## Risks / Trade-offs

- **Over-tuned brake feels instantaneous / under-tuned feels inert** → the constants
  are first-cut estimates. Mitigation: tests assert the qualitative "rest within a
  couple of seconds" rather than an exact time, and the constants are isolated in
  `RideParameters` for easy trimming against the running twin.
- **Brake fighting a residual drive** → if power were somehow non-zero while braking,
  a strong motor torque could resist the brake. Mitigation: `EngageBrakes()` cuts
  power to zero, and while engaged the UI disables the sliders, so no drive command
  arrives; the physics still applies the brake as a loss regardless.
- **Frontend/backend field drift** → the new `brakesEngaged` wire field must map
  through `mapRideTelemetry`. Mitigation: follow the exact pattern the recent
  `reverse-ride-direction` change used for its new telemetry field, with a frontend
  spec asserting the mapping.
- **Breaking the one-shot endpoint** → any external caller of the old no-body POST
  breaks. Mitigation: the only caller is this repo's frontend, updated together; the
  request body defaults `Engaged` to `false` so a bodyless POST is still well-defined
  (releases the brake) rather than failing.

## Migration Plan

Additive on the domain/telemetry side (new constants, new state, new telemetry
field defaulting to released); the one behavioural break is the engine-brake command
becoming stateful, resolved by shipping backend and frontend together. The ride is
ephemeral and in-memory, so there is no data migration. Rollback is reverting the
change; no persisted state is affected.

## Open Questions

- Should the manual brake toggle be reachable only while `Started` (matching the
  current button gating) or from any running state? Assumed **`Started` only** for
  the button gating to preserve current behaviour, while the underlying command
  itself remains state-agnostic; easily revisited in the frontend task without
  touching the contract.
