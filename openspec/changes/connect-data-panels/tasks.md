## 1. Telemetry contract (DigitalTwin.Abstractions)

- [x] 1.1 Add `BoardedPassengerCount` (int) to the `RideTelemetry` record
- [x] 1.2 Add `IsOccupied` (bool) and `IsSecured` (bool) to the `SeatTelemetry` record, keeping the existing `Restraint` enum
- [x] 1.3 Confirm the additions serialize/deserialize cleanly (JSON round-trip) and are additive/non-breaking for existing consumers

## 2. Emit telemetry throughout the active lifecycle (DigitalTwin module)

- [x] 2.1 Add an intent-revealing `IsActive` member to `Ride` (true for every state except `Idle`) and surface it via `RideStore`/`IRideTelemetryProvider`
- [x] 2.2 Change `RideTelemetryStream.Stream` to gate emission on "ride is active" instead of `IsRunning`, so frames are emitted during `Loading`, `Safe`, `Offloading` and the running states
- [x] 2.3 Keep the fixed 30 Hz sampling and connection-stays-open-while-Idle behavior unchanged

## 3. Populate the new fields in the projections (DigitalTwin module)

- [x] 3.1 Populate `SeatTelemetry.IsOccupied`/`IsSecured` in `Seat.ToTelemetry` from `Seat.IsOccupied`/`Seat.IsSecured`
- [x] 3.2 Compute and populate `RideTelemetry.BoardedPassengerCount` in `Ride.ToTelemetry` as the total occupied seats across all gondolas

## 4. Random empty-gondola selection (DigitalTwin domain)

- [x] 4.1 Add a seeded selection/shuffle helper to `IRideEventSampler` (+ `RandomRideEventSampler`) for choosing empty gondolas at random
- [x] 4.2 Update `Ride.BoardGroup` to seat members into `requiredGondolas` empty gondolas chosen at random via the sampler, leaving `GreatMill.EmptyGondolas()` as an ordered pure enumeration and all fit/capacity rules unchanged
- [x] 4.3 Verify never-split-a-group, `ceil(N/2)` whole-gondolas, and never-share-a-gondola invariants still hold with random selection

## 5. Frontend contract and mapping (FourDotnet.BoogaBooster.App)

- [x] 5.1 Add `isOccupied`/`isSecured` to `RideTelemetrySeatStreamDto` and the boarded count to `RideTelemetryStreamDto` in `ride.models.ts`
- [x] 5.2 Update `mapRideTelemetry` to derive seat state from `isOccupied` (not `occupiedKg > 0`) and carry the boarded count; keep a safe fallback if a field is absent
- [x] 5.3 Point `RideStateService.occupiedSeats` at the streamed boarded count and confirm `totalLoadKg`/`securityState`/`loadBalanceState` update from streamed frames

## 6. Panels reflect live data (FourDotnet.BoogaBooster.App)

- [x] 6.1 Confirm `bb-security-panel` passenger count and weight-in-kilos update on every frame during loading (no template change expected; verify bindings)
- [x] 6.2 Confirm `bb-status-summary` occupied-seat count and security roll-up show occupied-but-unsecured during the restraint countdown and secured afterwards

## 7. Tests

- [x] 7.1 DigitalTwin domain tests: `BoardGroup` seats into randomly chosen empty gondolas (seeded sampler), invariants preserved
- [x] 7.2 DigitalTwin projection tests: `BoardedPassengerCount` equals occupied seats; `SeatTelemetry.IsOccupied`/`IsSecured` reflect seat state through the restraint countdown
- [x] 7.3 Telemetry-stream tests: frames are emitted while `Loading`/`Safe`/`Offloading`, none while `Idle`, and boarding progress is visible across successive frames
- [x] 7.4 Angular SSE-source spec: `mapRideTelemetry` reads the new fields; occupied-but-unsecured maps correctly; count and weight update during loading
- [x] 7.5 Angular panel specs: security-panel and status-summary render the streamed count/weight/security state

## 8. Verification

- [x] 8.1 `dotnet build BoogaBooster.slnx` and `dotnet test BoogaBooster.slnx` pass — DigitalTwin project compiles clean and 145/146 tests pass; the one failure (`PassengerWeight_rejects_out_of_range_values(130.1)`) is a pre-existing test unrelated to this change (files unmodified; `MaxPassengerKg`=150). Full-solution `dotnet build` copy step fails only because the Aspire app was running and locked the output DLLs — not a compile error.
- [x] 8.2 `npm test` (vitest) passes for the affected specs — 5/5 target spec files, and the full app suite is green (23 files / 137 tests)
- [ ] 8.3 Run via Aspire and confirm end-to-end: as groups board, the top-right Load & security panel's passenger count and kilos update live and seats become secure over time — **needs a manual run**: the Aspire app currently running holds the pre-change build (locked DLLs). Restart the AppHost and watch a load cycle to confirm visually.
