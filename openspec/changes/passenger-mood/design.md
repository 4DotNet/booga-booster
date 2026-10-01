## Context

Today a guest is a `Person` (number, name, weight) in the Queue module. On boarding, `RideLoadingCoordinator` keeps only the weight (`PassengerWeight`), so the DigitalTwin has no idea who sits where, and `Gondola.Offload()` discards passengers in a single tick. Gondolas compute signed `ForwardG`/`LateralG` (horizontal only); `RideParameters.MaxGForce = 4.5` exists but nothing reads it. The queue has no per-group timestamp and no tick of its own (the filler runs every 10–60 s). The 3D scene (`ride-visualization.ts`) dead-reckons the rig from rpm, has no people, and is `aria-hidden`.

Guiding principle for this change: **KISS** — no new events, services, background loops or backend state that only the frontend needs.

## Goals / Non-Goals

**Goals:**
- Mood values per guest that survive queue → seat → exit.
- Deterministic, testable mood rules with every constant named and justified.
- A readable Rider Experience panel and a fun, cheap park scene.

**Non-Goals:**
- Mood affecting behaviour (no guests leaving the queue, no refusing to board, no demands to stop).
- On-ride happiness *loss* (only queue waits lower happiness).
- Puke as twin state, janitors, or cleaning.
- Making the scene a faithful replay of backend gondola angles.
- Enforcing the over-G interlock from `docs/05` (that belongs to a safety change, cf. `passenger-safety`).

## Decisions

### D1 — Queue decay is computed on read, not ticked
`QueuedGroup` stores `EnqueuedAt` (from `TimeProvider`). `RideQueueService` applies `happiness = start − 5 × (e^(max(0, wait − 5 min) / 5 min) − 1)`, clamped, when it maps to DTOs (`GetStatus`, `TakeGroupAsync`). The person keeps only their *starting* happiness.
*Alternative:* a hosted loop mutating happiness every second — more moving parts, harder to test, same result.

### D2 — Felt G per gondola, per docs §5.1
`Gondola.UpdateGForces` additionally sets `FeltG = √(1 + ForwardG² + LateralG²)`. Riders in the same gondola feel the same G (seat offsets are ignored — the 0.45 m difference is negligible for mood).

### D3 — On-ride mood is advanced inside the gondola physics step
After `UpdateGForces`, `Gondola` advances each seated passenger's mood with `dt`:
- happiness `+= 2 × (1 − d / 1.0 g) × dt` when `d = |FeltG − PreferredG| < 1.0 g`;
- nausea `+= 0.1 × (nausea + 5) × dt` when `FeltG − PreferredG > 0.3 × MaxGForce`;
- a per-gondola `_overLimitSeconds` accumulator: when it first crosses 1 s, +25 nausea to its riders; it resets to 0 when `FeltG < MaxGForce`.
Only runs while physics runs (Started / Stopping / EmergencyStop), so loading/idle time does not change ride mood. Pure function of state and `dt` → determinism preserved.
*Why these numbers:* 2 pts/s means a perfectly matched 30 s ride adds about +60 — clearly visible. A 1 g band means "close" is roughly the swing between the docs' 1.0 g low and 2.8 g peak, split in half. The nausea seed 5 and rate 0.1/s take an overshooting rider from 0 to the sick threshold (70) in about 27 s — one ride's worth of abuse. All go in `RideParameters` with rationale in `docs/`.
*Alternative:* a separate `RiderMoodService` iterating seats after `Advance` — duplicates the G lookup and splits physics-step logic.

### D4 — Passenger carries identity; boarding passes passengers, not weights
`Passenger` gains `GuestNumber`, `Happiness`, `PreferredG`, `Nausea` (weight stays). `IRideStore.BoardGroup` / `Ride.BoardGroup` take passenger seeds built from `PersonDto` in `RideLoadingCoordinator`. Mood mutation goes through intention-revealing methods on `Passenger` (follow the `csharp-domain-model` skill / style guide for state mutation).

### D5 — The last offload is a snapshot, not an event
Before `_mill.Offload()` clears seats, `Ride` captures the riders into `LastOffload { Counter, Riders[] }`. Telemetry always carries it (≤ 32 entries). The frontend reacts when `Counter` changes.
*Alternative:* a `riders-disembarked` integration event — nobody else consumes it yet, and the frontend reads SSE, not the bus.

### D6 — Averages and thresholds live in the frontend
Telemetry carries raw per-seat values; queue status carries raw per-guest values. The frontend computes the four averages and the mad (< 30) / sick (≥ 70) classification in one `mood.models.ts`. The thresholds are presentation rules, so the backend never needs them.

### D7 — Park scene layout (static, hand-placed)
Ride sweep radius ≈ 6 + 2 + 1 = 9 m. Entry booth at about 12 m on one side, exit booth at about 12 m at 90° from it, both facing the rig. The queue path is a fixed switchback behind the entry booth: 4 rows × 15 slots, 0.6 m apart, rows 1 m apart (60 slots). The exit path is a straight 10 m walk away from the exit booth, after which walkers are removed. Booth = box + striped cone roof (alternating-colour canvas texture) + coloured sign: entry green, exit red.

### D8 — One pair of instanced meshes for every figure
A `CapsuleGeometry` body `InstancedMesh` and a `SphereGeometry` head `InstancedMesh`, capacity 160 (60 queue + 32 riders + 32 exiting + 36 walking into the entry). Per frame, every live figure writes its matrix; colours use `setColorAt` (shirt = palette[guestNumber mod 8], head = mood colour, LEGO yellow `#F2CD37`). Seated riders take their matrix from the gondola pod's `matrixWorld` × a seat offset, so they follow the (approximate) scene motion. Telemetry gondola `(hubIndex, index)` maps to scene `(k, j)`. Puddles are a third `InstancedMesh` (flat green discs, capacity 32) whose scale shrinks to 0 over 20 s ("drying up") — avoids per-instance opacity. Puddle position is a deterministic offset from the guest number near the exit path.

### D9 — Walking is client-side interpolation
Each figure has `position` and `target`. On a queue update, targets are reassigned by queue order; figures move toward targets at 1.4 m/s. Guests that disappeared from the queue get the entry booth as target and are removed on arrival. Under `prefers-reduced-motion`, `position = target` immediately. The `+N` overflow label is a `Sprite` with a `CanvasTexture`, redrawn only when N changes.

### D10 — Scene logic is split into a pure, tested module
`park-scene.ts` holds pure functions (slot positions, overflow count, head/shirt colour, target assignment, puddle placement, walk step) with Vitest specs. `ride-visualization.ts` only wires them to Three.js, keeping the WebGL path thin (WebGL is unavailable under jsdom).

### D11 — DTOs move to the mandated layout while touched
`PersonDto`, `QueuedGroupDto` and `QueueStatusDto` move to `Queue.Abstractions/DataTransferObjects/<Feature>/` (via the `dto-organization` skill) as part of adding the mood fields, per CLAUDE.md's "fix when you touch it" rule.

## Risks / Trade-offs

- [The ride may never reach 4.5 g, or `passenger-safety`'s rpm trip may stop it first] → the max-G penalty simply never fires; that is acceptable. A spike task measures peak felt G at full power and records it in `docs/`. `MaxGForce` is a safety limit and is not tuned for mood.
- [High-preference riders (≈ 4 g) are rarely satisfied if the ride peaks around 3 g] → accepted ("thrill-seekers always want more"); documented with the spike result.
- [Scene gondola positions don't match backend physics] → riders ride the scene's gondolas; mood comes from telemetry. Fine for a decorative scene.
- [Telemetry payload grows (32 seats × 4 fields + offload list) at 30 Hz] → a few KB/s; acceptable for a local twin.
- [Queue updates every 5 s make walking look stepwise] → interpolation hides it; walking speed is chosen so a full slot shift finishes well within 5 s.
- [Domain `Passenger` becomes mutable] → mutation only through named methods, clamped in one place.
