## 1. Consult the style guide

- [x] 1.1 Query the `4dotnet-csharp-style-guide` MCP server for ADR-0003 (domain model), ADR-0005/0006 (CQRS feature slices), ADR-0007 (endpoints in module), and the unit-testing guideline before writing any C#.

## 2. Replace the RideState enum (Abstractions)

- [x] 2.1 Replace the values in `DigitalTwin.Abstractions/RideState.cs` with `Idle, Loading, Safe, Started, Stopping, Offloading, EmergencyStop` and rewrite the XML doc to describe the state machine.
- [x] 2.2 Add `IReadOnlyList<RideState> AvailableTransitions` to `DigitalTwin.Abstractions/RideTelemetry.cs` (with XML doc) as the operator-triggerable target states legal right now.

## 3. Build the state machine on the Ride aggregate

- [x] 3.1 Define the operator transition table in `Domain/Ride.cs`: a static map of `(from, to)` edges, each with an optional guard predicate (`IsSafe`) — `Idle→Loading`, `Loading→Safe` (guard), `Safe→Loading`, `Safe→Started` (guard), `Started→Stopping`, and `{Loading,Safe,Started,Stopping}→EmergencyStop`.
- [x] 3.2 Add `RequestTransition(RideState target)`: reject an undefined `(current, target)` pair or a failing guard with `DomainValidationException`; otherwise set the state and run the entry side-effect. Call `MarkChanged()`.
- [x] 3.3 Add private intent-revealing entry helpers: `EnterStarted` (lock safety constraints, release gondola brakes), `EnterStopping` (cut power, apply brakes), `EnterEmergencyStop` (immediately cut power, apply brakes), `EnterOffloading` (release safety constraints), `EnterIdle`.
- [x] 3.4 Add restraint locking/unlocking so constraints lock on entering `Started` and only release on entering `Offloading`; ensure occupied restraints cannot open while locked.
- [x] 3.5 Replace `Start()`/`Stop()` public methods (and `UpdateReadiness`) with the state-machine equivalents; update `BoardPassenger` to permit boarding only in `Idle`/`Loading` (no `Ready`).
- [x] 3.6 Implement automatic transitions inside `Advance`: `Safe→Loading` when `!IsSafe`; `Stopping→Offloading` and `EmergencyStop→Offloading` when `Mill.IsAtRest` (engage gondola brakes, release constraints); `Offloading→Idle` when every seat is empty.
- [x] 3.7 Add an `AvailableTransitions` projection: the operator edges from the current state whose guard passes (excluding automatic edges).
- [x] 3.8 Update `ToTelemetry()` to emit the new state and `AvailableTransitions`; keep `IsSafeToStart`/`SafetyReason` consistent with the `Safe`/`Started` guards.

## 4. Feature slice, store, and endpoint

- [x] 4.1 Add `IRideStore.RequestStateTransition(RideState target)` and implement it in `RideStore` (mutate under the existing thread-safe boundary, return the resulting telemetry).
- [x] 4.2 Create the feature slice `Features/RequestRideStateTransition/` with `RequestRideStateTransitionCommand(RideState Target)` and its `CommandHandler` dispatching to the store (mirror `StartRideCommandHandler`).
- [x] 4.3 Register the handler in `DigitalTwinModuleExtensions` alongside the existing handlers.
- [x] 4.4 Add `POST /ride/state` to `DigitalTwinEndpoints`: parse the target state (reject unknown names with 400), dispatch the command via `DispatchAsync` (illegal/guard-failing transitions surface as 400 through `DomainValidationException`).
- [x] 4.5 Remove or retire the now-superseded `/ride/start` and `/ride/stop` endpoints/features if fully replaced by `/ride/state`, or leave them delegating to the machine — decide and note in the endpoint.

## 5. Backend tests (DigitalTwin.Tests, xUnit v3)

- [x] 5.1 Cover the transition table: each legal operator transition is accepted; representative illegal pairs are rejected and leave state unchanged.
- [x] 5.2 Cover guards: `Loading→Safe` and `Safe→Started` blocked when unsafe, allowed when safe.
- [x] 5.3 Cover automatic transitions via `Advance`: `Safe→Loading` on safety loss, `Stopping→Offloading` at rest, `EmergencyStop→Offloading` at rest (constraints released), `Offloading→Idle` when empty.
- [x] 5.4 Cover Emergency Stop from each active state and its rejection from `Idle`; assert power cut and brakes applied.
- [x] 5.5 Assert `AvailableTransitions` content per state (e.g. `Safe` when safe → `Started, Loading, EmergencyStop`; omits `Safe` from `Loading` when unsafe).
- [x] 5.6 Endpoint test: `POST /ride/state` accepts a legal transition, rejects an illegal one (state unchanged) and an unknown state name with 400.
- [x] 5.7 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` are green; module coverage stays ≥ 80%.

## 6. Frontend model and lifecycle service

- [x] 6.1 In `ride-dashboard/models/ride.models.ts` replace the `RideState` union with `'idle' | 'loading' | 'safe' | 'started' | 'stopping' | 'offloading' | 'emergency-stop'`; add `availableTransitions: readonly RideState[]` to `RideTelemetry`; add a `request-state-transition` command to `RideCommand`.
- [x] 6.2 Add mapping helpers from the backend PascalCase `RideState` to the frontend union and back (for the POST body).
- [x] 6.3 Create `ride-dashboard/state/ride-lifecycle.service.ts` (`providedIn: 'root'`): poll `GET /api/ride/telemetry`, expose `state` and `availableTransitions` signals, and `requestTransition(state)` posting to `POST /api/ride/state` then refreshing (mirror `HttpQueueSource`); use `inject()`, signals, `takeUntilDestroyed`.
- [x] 6.4 Provide the service and confirm `/api` proxying already covers the new endpoint (reuse the queue/weather proxy setup).

## 7. Status-summary panel and dashboard wiring

- [x] 7.1 Retype the panel's `state` input to the new `RideState`; add an `availableTransitions` input and a `transition` output (`output<RideState>()`).
- [x] 7.2 Render a button per lifecycle state; enable a button only when its state is in `availableTransitions`; mark the current state with `aria-current`/text; convey enabled/disabled by more than colour; emit `transition` on click.
- [x] 7.3 In `ride-dashboard`, inject `RideLifecycleService`, feed its `state`/`availableTransitions` into the panel, and wire the `transition` output to `requestTransition`.
- [x] 7.4 Keep the `stateLabel` human-readable for the seven states (e.g. "Emergency stop").

## 8. Frontend tests and accessibility

- [x] 8.1 Update/extend `status-summary` and `telemetry-panels` specs: correct buttons lit per `availableTransitions`, disabled buttons emit nothing, clicking a lit button emits the right `transition`.
- [x] 8.2 Add/extend a lifecycle-service spec (fake HTTP): polling projects state/transitions, `requestTransition` posts the mapped body and refreshes.
- [x] 8.3 Update the ride-dashboard a11y spec so the button grid passes AXE / WCAG AA.
- [x] 8.4 `npm test` (vitest) is green.

## 9. Verify end-to-end

- [x] 9.1 Run the backend via Aspire and the Angular app; drive `Idle → Loading → Safe → Started → Stopping → Offloading → Idle` from the panel and confirm the server state and lit buttons update accordingly.
- [x] 9.2 Trigger Emergency Stop from `Started` and confirm brakes apply, the ride comes to rest, constraints release, and it lands in `Offloading` then `Idle`.
- [x] 9.3 Confirm illegal transitions are not offered (buttons unlit) and a forced illegal `POST /ride/state` returns 400 without changing state.
