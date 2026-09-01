## Why

The ride currently advances through an ad-hoc set of lifecycle states (`Idle, Boarding, Ready, Running, Stopping, Faulted`) with the transition rules scattered across individual methods on the `Ride` aggregate, and the dashboard only understands a simplified three-value state (`stopped/running/emergency`) driven entirely by a client-side simulator. There is no single, enforceable definition of which state the ride may move to next, and no operator surface for driving those transitions. A physical ride must never be able to follow a path that leaves passengers in an unsafe situation (e.g. spinning before restraints are locked, or releasing restraints before it has stopped). We need an authoritative state machine that owns the legal transitions and their safety guards, and a dashboard panel that reflects it and drives it.

## What Changes

- **BREAKING** Replace the ride lifecycle states with the seven operator-facing states: **Idle, Loading, Safe, Started, Stopping, Offloading, Emergency Stop**. The old `RideState` values (`Boarding, Ready, Running, Faulted`) are removed.
- Introduce an explicit, guarded **state machine** on the `Ride` aggregate: a single place that decides which transitions are legal from the current state, enforces the safety guards on each, and runs the entry side-effects (locking/releasing safety constraints, applying brakes, cutting power).
- Distinguish **operator-triggered** transitions (driven by a button / server command) from **automatic** transitions the simulation performs when a physical condition is met (`Stopping → Offloading` once at rest, `Offloading → Idle` once the last rider has left, `Emergency Stop → Offloading` once at rest with constraints released).
- Add an **Emergency Stop** path reachable from every active state that immediately applies brakes and, on coming to rest, releases the safety constraints.
- Expose the current state **and the set of currently-legal operator transitions** in the ride telemetry, and add a **server endpoint** to request a state transition (rejected by the domain if illegal/unsafe).
- Wire the **status-summary panel** to the backend over HTTP: it shows the live ride state and a button per state, with only the currently-legal transitions lit; clicking a lit button posts the transition to the server, and the panel re-renders from the server's new state.
- **Scope note (deferred):** `Loading` is the boarding window in this change; automatically dequeuing groups from the Queue module and auto-boarding them is a follow-up. The full physics telemetry pipeline remains on the client-side simulator — only the ride lifecycle state and transitions are backed by the server here.

## Capabilities

### New Capabilities

- `ride-state-machine`: the authoritative lifecycle of the ride — the seven states, the legal transitions between them, the safety guards and entry side-effects on each transition, the automatic (condition-driven) transitions, exposure of the current state and legal transitions in telemetry, the server endpoint that accepts a transition request, and the dashboard panel that displays and drives the machine.

### Modified Capabilities

<!-- No existing published specs (openspec/specs/ is empty); all behavior here is introduced by the new capability above. -->

## Impact

- **DigitalTwin module (backend):**
  - `RideState` enum (Abstractions) — **breaking** rename/replacement of the state set.
  - `Ride` aggregate (`Domain/Ride.cs`) — replace scattered transition logic with the guarded state machine; add `RequestTransition`, automatic transitions in `Advance`, and an `AvailableTransitions` projection.
  - `RideTelemetry` (Abstractions) — add `AvailableTransitions`.
  - New feature slice `Features/RequestRideStateTransition/` (command + handler), a new endpoint `POST /ride/state`, and a new `IRideStore` operation.
  - Unit tests for the DigitalTwin module (legal/illegal transitions, guards, automatic transitions).
- **Angular app (frontend):**
  - `ride.models.ts` — replace the `RideState` union with the seven states, add `availableTransitions`, add a request-transition command.
  - New backend-backed `RideLifecycleService` (poll telemetry + post transition) and provider wiring in `app.config.ts`.
  - `status-summary` panel — add the state-button grid (lit per available transitions, accessible), plus a transition output; `ride-dashboard` wires it to the lifecycle service.
- **Consumers of `RideState`** (any code/tests referencing the removed values) must be updated.
