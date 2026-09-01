## Context

The DigitalTwin module already models the ride as a rich `Ride` aggregate (ADR-0003) that owns a `GreatMill` → hubs → gondolas → seats, advances on a fixed timestep via a `RideSimulationService` (`BackgroundService`), and is fronted by a CQRS feature-slice layer (ADR-0005/0006) with minimal-API endpoints living in the module (ADR-0007). Telemetry is exposed via `GET /ride/telemetry` as an immutable `RideTelemetry` snapshot.

Today the lifecycle logic is spread across `Ride.Start()`, `Ride.Stop()`, `Ride.BoardPassenger()`, `Ride.Advance()`, and a private `UpdateReadiness()`; the states are `Idle, Boarding, Ready, Running, Stopping, Faulted`. There is no single description of "what may follow what," and Emergency Stop and Offloading do not exist. The Angular dashboard reads a client-side simulator through the `RIDE_TELEMETRY_SOURCE` seam and understands only `stopped/running/emergency`; the status-summary panel is read-only.

Per the decisions taken for this change: (1) **replace** the state set with the seven requested states; (2) `Loading` is only the boarding window — Queue dequeue/auto-board is a follow-up; (3) the ride **lifecycle** state and its transitions are backed by the real server over HTTP, while the physics telemetry stays on the client simulator.

**IMPORTANT:** all C# here must follow the `4dotnet-csharp-style-guide` MCP server — ADR-0003 (rich domain model, intent-revealing methods, `DomainValidationException`), ADR-0005/0006 (CQRS feature slices), ADR-0007 (endpoints in the module), and the xUnit v3 / Moq / Bogus testing guideline (no FluentAssertions). Consult it before writing code.

## Goals / Non-Goals

**Goals:**

- One authoritative state machine on the `Ride` aggregate that decides legal transitions, evaluates guards, and runs entry side-effects — the single home for the lifecycle safety invariants.
- Clear separation of **operator-triggered** transitions (server commands) from **automatic** transitions (performed in `Advance` when a physical condition is met).
- Telemetry that carries the current state **and** the currently-legal operator transitions, so the client never re-implements the table.
- A server endpoint to request a transition, and a dashboard panel that shows the state, lights only the legal transitions, and drives them over HTTP.

**Non-Goals:**

- Dequeuing groups from the Queue module or auto-boarding them (follow-up).
- Moving the full physics telemetry (mill/hubs/gondolas/g-forces) onto the backend feed — only lifecycle state + transitions are server-backed here; the simulator keeps driving the physics panels.
- Persisting the ride or its state across restarts (the ride stays ephemeral and in-memory).
- Motor-power gating semantics beyond what the state machine needs (see the motion invariant decision below).

## Decisions

### 1. A single guarded `RequestTransition(RideState target)` on the aggregate, backed by an internal transition table

The `Ride` keeps its invariants in one place. Rather than one public method per edge (`BeginLoading`, `MarkSafe`, …), the endpoint receives a **target state** from a button, so a single guarded entry point maps cleanly to the UI. `RequestTransition` looks the (current, target) pair up in a static transition table; each entry carries an optional guard (a predicate over the current ride, e.g. `IsSafe`) and an entry action (the side-effects). Illegal pair or failing guard → `DomainValidationException` (translated to 400 by the existing `DispatchAsync`).

- *Alternative — one intent method per transition:* more idiomatic ADR-0003 "intent-revealing" naming, but forces the endpoint/handler to switch on the target state and duplicates the table across the boundary. Rejected: the table is the invariant; keep it in one place. Private per-transition helper methods (`EnterStarted()`, `EnterOffloading()`) still give intent-revealing side-effect code.
- *Alternative — a general-purpose state-machine library:* over-kill and disallowed in spirit by the "hand-written base classes, no external CQRS/mediator libraries" stance; the table is a handful of edges.

### 2. Operator transitions vs. automatic transitions

Operator transitions are only those a human drives via a button; automatic ones are physical consequences the simulation applies itself:

- **Operator:** `Idle→Loading`, `Loading→Safe` (guard: `IsSafe`), `Safe→Loading`, `Safe→Started` (guard: `IsSafe`), `Started→Stopping`, and `{Loading,Safe,Started,Stopping}→EmergencyStop`.
- **Automatic (in `Advance`):** `Safe→Loading` when `!IsSafe`; `Stopping→Offloading` and `EmergencyStop→Offloading` when `Mill.IsAtRest`; `Offloading→Idle` when every seat is empty.

`AvailableTransitions` (exposed in telemetry) is computed as the operator edges from the current state whose guard passes — automatic edges are deliberately excluded so buttons never offer a transition the machine will make on its own.

### 3. Entry side-effects map onto the existing mill/gondola/restraint operations

- `Started`: lock safety constraints (restraints) and release the gondola yaw brakes so the pods swing — reuses the existing `_mill.ReleaseAllGondolaBrakes()` and adds restraint locking.
- `Stopping`: `_mill.CutAllPower()` and apply brakes; coast/brake to rest (existing behavior).
- `EmergencyStop`: immediately `_mill.CutAllPower()` and apply brakes — reachable from any active state.
- `Offloading`: release the safety constraints (only ever done at rest).
- On reaching rest in `Stopping`/`EmergencyStop`, engage gondola brakes as today, then advance to `Offloading`.

The **motion invariant** ("the ride is only in motion while Started/Stopping/EmergencyStop") is preserved by construction: the mill only carries power/momentum once `Started` releases the brakes, and every stop path cuts power. We do not add throttle-gating to the power sliders in this change (they remain operator inputs); we note it as a future hardening item rather than expand scope.

### 4. `RideState` enum replacement is a breaking, compile-time-guided edit

Replacing `Boarding/Ready/Running/Faulted` with `Loading/Safe/Started/Offloading/EmergencyStop` breaks every reference. This is intentional and desirable — the compiler enumerates the call sites (aggregate, telemetry projection, handlers, tests). `Idle` and `Stopping` keep their names. The XML doc on the enum is rewritten to describe the machine.

### 5. Telemetry carries `AvailableTransitions`; the client renders from it

`RideTelemetry` gains `IReadOnlyList<RideState> AvailableTransitions`. The client maps the backend `RideState` (PascalCase) to its own kebab/lowercase union and lights exactly the buttons present in that list. This keeps the transition table server-authoritative — the panel is a pure projection and never decides legality itself.

### 6. Frontend: a dedicated backend-backed `RideLifecycleService`, presentational panel

To satisfy "state changes are sent to the server, the panel updates from the server" without rewriting the whole physics telemetry pipeline, the lifecycle is wired through a **new** `RideLifecycleService` (`providedIn: 'root'`) that mirrors the existing `HttpQueueSource` pattern: poll `GET /api/ride/telemetry`, expose `state` and `availableTransitions` signals, and `requestTransition(state)` posts to `POST /api/ride/state`, then refreshes. The `status-summary` panel stays presentational — it gains `state` (already an input, retyped), an `availableTransitions` input, and a `transition` output; `ride-dashboard` wires the output to `RideLifecycleService.requestTransition` and feeds its signals in. The physics panels keep reading the simulator via `RideStateService`/`RIDE_TELEMETRY_SOURCE`, untouched.

- *Alternative — swap `RIDE_TELEMETRY_SOURCE` for a full `HttpRideTelemetrySource`:* one source of truth, but forces mapping the entire physics snapshot (hubs, gondolas, g-forces, per-seat state, brake) over HTTP now — far beyond the state machine. Rejected as scope creep; the seam is left for that follow-up.

### 7. Accessibility of the button grid

Buttons use the native `disabled` attribute (not colour) for un-lit states; the current ride state is marked with `aria-current` / `aria-pressed` and text, never colour alone, matching the existing panel's approach (icon + text for security). The grid is keyboard-navigable and labelled. AXE/WCAG AA must pass (a spec file exists for panel a11y patterns).

## Risks / Trade-offs

- **Two notions of "state" on the client (simulator's `stopped/running` vs. backend lifecycle)** → The retyped `RideState` is the lifecycle; the simulator's derived motion is a separate concern surfaced by the physics panels (speeds). We keep the lifecycle strictly on `RideLifecycleService` so the panel's state comes only from the server. Document this seam so the follow-up can unify them.
- **Polling latency makes the button feel laggy** → `requestTransition` triggers an immediate refresh after the POST (as `HttpQueueSource.refresh` does) so the panel reflects the new state on the next tick rather than waiting a full poll interval; optionally optimistic-update the state locally on a 2xx.
- **Automatic transitions racing operator requests** (e.g. operator clicks `Safe` the same tick safety is lost) → The domain is the arbiter: `RequestTransition` re-evaluates the guard at apply time under the store's existing thread-safe boundary, so a stale button click is simply rejected. The client tolerates a rejected POST (state just doesn't change).
- **Breaking `RideState` ripples into unforeseen consumers** → Compile-driven; run `dotnet build` early to enumerate. The frontend `RideState` is a separate type — its consumers (status-summary, ride-state.service, specs) are updated together.
- **Motion invariant not enforced on the throttle** → Power sliders can still command the motors outside `Started` in this change. Mitigation: the state machine's stop paths cut power, and the invariant is documented; throttle-gating is a scoped follow-up, not a regression from today's behavior.

## Migration Plan

1. Backend first: replace the enum, build the state machine on `Ride`, add `AvailableTransitions` to telemetry, add the feature slice + endpoint + store op, update/extend tests. `dotnet build` + `dotnet test` green.
2. Frontend: retype `RideState`, add `RideLifecycleService`, extend the panel + dashboard wiring, update specs. `npm test` green, AXE clean.
3. No data migration (ephemeral ride). Rollback is a straight revert; nothing is persisted.

## Open Questions

- Should the power sliders be disabled/ignored outside `Started` (full motion-gating)? Deferred; flagged as follow-up hardening.
- Should `Loading` require the Queue to be non-empty before it can be entered? Deferred with Queue integration.
