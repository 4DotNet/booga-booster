## Context

The ride dashboard's top-right "Load & security" panel (`bb-security-panel`) shows a wrong boarded-passenger count and a weight in kilos that never updates; the top-left status summary shows zero occupied seats during boarding. Investigation traced this to two structural facts, not a wiring typo in the panels:

1. **The SSE telemetry stream is silent during boarding.** `RideTelemetryStream.Stream` yields a frame only when `_store.IsRunning`, i.e. only in `Started`/`Stopping`/`EmergencyStop` (`RideTelemetryStream.cs:38`). Passengers board and restraints secure during `Loading` and `Safe`, so no frame is emitted while these numbers are actually changing. The frontend's `telemetry` signal stays on its `atRestTelemetry()` seed — every seat empty, 0 kg — until the ride starts running.
2. **The frontend reconstructs load and count purely from `seat.occupiedKg`.** `mapRideTelemetry` reads only `seat.occupiedKg` and `seat.restraint`; `seatState()` forces any seat with `occupiedKg === 0` to `'empty'`. There is no boarded-passenger *count* field on the wire at all — the domain never models one (only per-seat/gondola/mill weights exist). So the count is inferred, and the aggregate `mill.passengerLoadKg` / `gondola.loadKg` fields the DTO already carries are never read.

Separately, the requested boarding *feel* — "grab a random gondola, take a seat, it takes time to become safe" — is only half implemented: the restraint-close countdown (10–30 s, `Seat.AdvanceNaturalBehavior`) exists and works, but gondola selection is deterministic (`GreatMill.EmptyGondolas()` walks hubs in fixed order), and the securing process is invisible over SSE because of gap (1).

Constraints: C# work follows the `4dotnet-csharp-style-guide` — rich domain models with private setters and intent-revealing methods (ADR-0003), telemetry DTOs stay `record`s in the `.Abstractions` project, and tests use xUnit v3 with Moq/Bogus (no FluentAssertions). Randomness introduced in the domain must remain deterministically testable.

## Goals / Non-Goals

**Goals:**
- Emit telemetry frames throughout the active lifecycle (`Loading`, `Safe`, `Offloading`, and the running states), so the panels observe boarding, load accumulation and restraints securing live.
- Add an explicit boarded-passenger count and per-seat occupied/secured indicators to the telemetry contract, so the frontend reports counts and security from streamed data instead of guessing from weight.
- Make group boarding seat members into randomly chosen empty gondolas, keeping all existing fit/capacity rules intact and keeping the behavior seedable for tests.
- Bind the security/status panels to the streamed count, weight and per-seat secured state so they update every frame.

**Non-Goals:**
- Changing the physics timestep, the telemetry sampling rate (stays 30 Hz), or the SSE endpoint URL/event name (`ride-telemetry`).
- Changing the restraint-close timing model or the `Loading → Safe → Started` promotion rules.
- Changing which groups fit / the look-ahead backfill logic in `RideLoadingCoordinator` (only *which* empty gondolas a fitting group lands in changes).
- Persisting ride state or supporting multiple concurrent rides.

## Decisions

### 1. Widen the emission gate to "active" (any non-Idle state)
Replace the `_store.IsRunning` check in `RideTelemetryStream.Stream` with an "is the ride active" check that is true for every state except `Idle`. Expose this as an intent-revealing member (e.g. `Ride.IsActive` / surfaced via the store) rather than testing the enum in the stream, keeping the lifecycle rule in the domain.

- *Why not emit always (including Idle)?* Idle is a genuine at-rest snapshot the frontend already renders from its seed; streaming identical idle frames at 30 Hz adds noise for no observable change. Starting frames the moment the ride leaves Idle is the meaningful boundary.
- *Why not a separate "emit on change" trigger?* The stream is already a fixed-rate sampler decoupled from physics (an existing requirement). Keeping fixed-rate sampling and only widening *when* it samples is the smallest change that satisfies "load/weight/gondola update ⇒ SSE frame," because at 30 Hz any change is reflected within one frame.

### 2. Add a boarded-passenger count + per-seat occupied/secured to telemetry
Add `BoardedPassengerCount` to `RideTelemetry`, and `IsOccupied` + `IsSecured` to `SeatTelemetry`. Populate them in the existing `ToTelemetry` projections from the domain (`Seat.IsOccupied`, `Seat.IsSecured` already exist; the ride count is the sum of occupied seats across gondolas).

- *Why an explicit count instead of inferring from `occupiedKg`?* Inference conflates "no passenger" with "passenger weight not yet known / zero" and is exactly what produces the current wrong count. A modeled count is unambiguous and directly testable against the spec ("count equals occupied seats").
- *Why surface `IsOccupied`/`IsSecured` rather than let the frontend interpret the `Restraint` enum?* The frontend needs "occupied but not yet secured" during the countdown; deriving that from the raw enum plus a weight threshold is the fragile logic we are removing. Surfacing the domain's own booleans keeps the contract honest. The raw `Restraint` enum stays for callers that want it.
- These are additive fields on records — non-breaking for deserialization; the frontend DTO/mapper adopts them.

### 3. Randomize empty-gondola selection through the existing sampler
Group boarding (`Ride.BoardGroup`) picks `requiredGondolas` empty gondolas. Change the selection from "first N in fixed order" to "N drawn at random from the empty set," routed through the existing `IRideEventSampler` (the same abstraction already used for passenger weights and restraint delays) so a seeded sampler makes tests deterministic.

- *Why the sampler and not `Random.Shared`?* The unit-testing guideline requires deterministic tests; the sampler is already injected and already the seam for all ride randomness. Adding a `NextGondolaSelection`/shuffle helper there keeps one randomness source.
- *Where to shuffle — `GreatMill.EmptyGondolas()` or `Ride.BoardGroup`?* Keep `EmptyGondolas()` as a pure, ordered enumeration (capacity math elsewhere depends on its count, not its order) and randomize at the point of *selection* in `BoardGroup`, so capacity/fit calculations are untouched and only seat assignment changes.

### 4. Frontend: read the new fields and rebind the panels
Extend `RideTelemetrySeatStreamDto` with `isOccupied`/`isSecured` and `RideTelemetryStreamDto` with the boarded count; update `mapRideTelemetry` to use `isOccupied` for seat state (not `occupiedKg > 0`) and to carry the count. Point `RideStateService.occupiedSeats` at the streamed count and keep `totalLoadKg` summing streamed seat weights (now non-zero during loading because of decision 1). No panel-template changes needed — the bindings already exist; they were just fed zeros.

- *Why not also read `mill.passengerLoadKg` for the weight?* Per-seat `occupiedKg` already sums to the same total and the frontend contract (and its specs) is per-seat; keeping one source avoids a second reconciliation. The aggregate mill/gondola fields remain available for future panels.

## Risks / Trade-offs

- **[Higher SSE volume — frames now stream during Loading/Safe/Offloading]** → Rate is unchanged (30 Hz) and payload size is unchanged aside from a few scalar fields; the extra traffic is only during phases that were previously silent, and the frontend already holds the connection open through them. No mitigation needed beyond confirming the reconnect logic is unaffected.
- **[Random gondola selection makes boarding non-reproducible in manual testing]** → All domain tests seed the sampler, so behavior is deterministic under test; only live runs vary, which is the intent.
- **[Additive contract fields could be missed by an out-of-date frontend build]** → Fields are optional/defaulted on the TS side; a frame lacking them degrades to the previous inference path rather than throwing. Ship backend and frontend together.
- **[Randomizing selection could interact with load-balance/imbalance safety]** → Imbalance is evaluated from actual seated weights regardless of *which* gondolas fill, so safety evaluation is unaffected; random distribution may actually surface imbalance scenarios sooner, which is acceptable.

## Open Questions

- Should the boarded-passenger count live only at ride level, or also per-gondola/per-hub? Ride-level satisfies the reported bug; per-hub could wait for a future panel. (Leaning ride-level only for now.)
- Do we also want `HubTelemetry.PassengerLoadKg` surfaced now (it exists in the domain but not the DTO), or defer until a panel needs it? (Leaning defer — out of scope for this fix.)
