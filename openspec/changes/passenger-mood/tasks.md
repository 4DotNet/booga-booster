## 1. Queue: guest mood and wait decay

- [x] 1.1 Move `PersonDto`, `QueuedGroupDto`, `QueueStatusDto` to `Queue.Abstractions/DataTransferObjects/<Feature>/` using the `dto-organization` skill; keep the build green
- [x] 1.2 Add `Happiness` (starting value), `PreferredG` and `Nausea` to the Queue `Person`, validated (0–100; preferred G within `[MaxGForce/2, MaxGForce]`)
- [x] 1.3 Draw happiness `[65, 85]` and preferred G `[2.25, 4.5]` in `PersonGenerator` from the seeded RNG; nausea 0; tests for range and seed determinism
- [x] 1.4 Add `EnqueuedAt` to `QueuedGroup`, set from `TimeProvider` in `RideQueueService`
- [x] 1.5 Add queue-wait decay constants (5 min grace, 5 pt scale, 5 min time constant) and a pure decay function; tests for the 4 / 10 / 16 minute scenarios and the 0 clamp
- [x] 1.6 Add `GuestNumber`, `Happiness` (decayed), `PreferredG`, `Nausea` to `PersonDto`; apply decay in `ToDto` for `GetStatus` and `TakeGroupAsync`; tests with `FakeTimeProvider`

## 2. DigitalTwin: identity, felt G and on-ride mood

- [x] 2.1 Add mood constants to `RideParameters.cs` (`HappinessGainPerSecond` 2, `FunBand` 1.0 g, `NauseaToleranceFraction` 0.3, `NauseaGrowthRate` 0.1, `NauseaSeed` 5, `MaxGPenaltySeconds` 1, `MaxGNauseaPenalty` 25); document derivations in a new `docs/` section and `docs/appendix-parameters.md`
- [x] 2.2 Extend `Passenger` with guest number, happiness, preferred G and nausea, plus named, clamping mutation methods; tests
- [x] 2.3 Change `IRideStore.BoardGroup` / `Ride.BoardGroup` to take passenger seeds; map from `PersonDto` in `RideLoadingCoordinator`; update existing boarding tests
- [x] 2.4 Compute `FeltG` in `Gondola.UpdateGForces`; tests for the at-rest and 2.24 g scenarios
- [x] 2.5 Advance rider mood in the gondola physics step (happiness gain, nausea growth); tests for the spec scenarios at the 1/120 s step
- [x] 2.6 Add the per-gondola over-limit accumulator and +25 penalty; tests for one stretch, two stretches and a short spike
- [x] 2.7 Capture `LastOffload { Counter, Riders }` in `Ride` before seats are cleared; tests
- [x] 2.8 Spike: drive the simulation at full mill and hub power in a test, measure peak felt G, and record the result (and whether `MaxGForce` is reachable) in `docs/`

## 3. Telemetry

- [x] 3.1 Add guest number, happiness, preferred G, nausea (nullable) to `SeatTelemetry` and `FeltG` to `GondolaTelemetry`
- [x] 3.2 Add `LastOffloadTelemetry` to `RideTelemetry`; map in the telemetry snapshot; endpoint/serialization tests
- [x] 3.3 Run the backend coverage check (`test-coverage` skill) — modules stay ≥ 80 % line coverage

## 4. Frontend: models and Rider Experience panel

- [x] 4.1 Map the new seat, gondola and last-offload fields in `ride.models.ts`; map queue groups and guests in `queue.models.ts` / `http-queue-source.ts`; update specs
- [x] 4.2 Add `mood.models.ts`: `MAD_HAPPINESS = 30`, `SICK_NAUSEA = 70`, `MAX_G_FORCE = 4.5`, average helpers (returning `null` for empty), mad/sick counters; specs
- [x] 4.3 Consult the `primeng` MCP server for `ProgressBar` API and accessibility, then build `rider-experience-panel` with the four bars and the three text counts; wire it under the Gondolas panel in `ride-dashboard.html`
- [x] 4.4 Vitest specs for the panel (averages, `—` empty state, `3.3 g` label, counts) and an `*.a11y.spec.ts` axe check

## 5. Frontend: park scene

- [x] 5.1 Create pure `park-scene.ts` (queue slot positions, overflow count, shirt colour, head colour with green-wins rule, target assignment, walk step, puddle placement and shrink) with Vitest specs
- [x] 5.2 Add entry and exit booths (box, striped cone roof, coloured sign) outside the 9 m sweep
- [x] 5.3 Add the instanced body/head meshes and draw the first 60 queued guests on the switchback path with the `+N` overflow sprite
- [x] 5.4 Animate walking between queue updates (1.4 m/s), boarded guests walking into the entry, and instant placement under reduced motion
- [x] 5.5 Seat rider figures in the gondolas (pod `matrixWorld` × seat offset) with mood head colours from telemetry
- [x] 5.6 On offload counter change, spawn exit walkers; spawn shrinking puddles for riders with nausea ≥ 70; remove walkers and puddles when done
- [ ] 5.7 Dispose the new geometries, materials and textures in `disposeScene`; check the scene manually in the running app (Aspire)
