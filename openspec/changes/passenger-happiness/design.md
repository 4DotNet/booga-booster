# Design

## Context

See `proposal.md` for motivation and `specs/` for the behavioural contract. The shape of the code today:

- **Queue → ride hand-off loses the person.** `RideLoadingCoordinator` (DigitalTwin) pulls groups through `IRideQueueService` (Queue.Abstractions), then maps each `PersonDto` to a bare `PassengerWeight`. `Ride.BoardGroup` builds a `Passenger(PassengerWeight)`, and offloading sets `Seat._occupant = null`. Any per-guest state has to survive that mapping.
- **No join time in the queue.** `QueuedGroup` has no timestamp. `RideQueueService` already has `TimeProvider` injected and uses it only for the `GroupQueuedIntegrationEvent`. The Queue module has no fine-grained tick. Its only timer is the 10–60 s filler cycle.
- **G-forces exist but are only horizontal.** `Gondola.UpdateGForces` computes `ForwardG` and `LateralG` every physics step (`Ride.Advance` in Started/Stopping/EmergencyStop). `RideParameters.MaxGForce = 4.5` is declared but no code uses it. The gondola panel's `VERTICAL_G_LIMIT = 4.5` mirrors it.
- **Telemetry** is `Ride.ToTelemetry()` → `RideTelemetry` (DigitalTwin.Abstractions), streamed over SSE at 30 Hz while the ride is not Idle.
- **Frontend.** The right rail in `ride-dashboard.html` holds the security, speed and gondola panels. Queue status is polled every 5 s by `HttpQueueSource`. PrimeNG is installed but not wired (`providePrimeNG` is absent).

## Goals / Non-Goals

**Goals:**

- Keep both simulations deterministic. Queue erosion is a pure function of `TimeProvider` time. Rider evolution is a pure function of state at the fixed 1/120 s step. All new randomness goes through the seeded `PersonGenerator`.
- Keep module boundaries intact. DigitalTwin learns about guest ratings only through `Queue.Abstractions` DTOs.
- Use closed-form, step-size-robust updates, so the behaviour does not drift if the timestep ever changes.

**Non-Goals:**

- Guests abandoning the queue, nausea feeding back into happiness, happiness recovering after a ride, and per-guest history after offloading.
- Any over-G *intervention*. 4.5 g is used only as the intensity reference and the episode threshold.
- Including gravity or a vertical component in the felt G magnitude. That would change the meaning of the existing gondola G readings and belongs with a future over-G interlock.

## Decisions

### D1 — Experience constants and their models

The constants on the ride side live in `RideParameters`. The constants on the queue side live as `const`s on a Queue domain type. All of them are derived in the new `docs/06-passenger-experience.md`.

| Symbol | Value | Meaning |
| --- | --- | --- |
| `QueuePatienceGrace` | 5 min | waiting time with no erosion |
| `τ_q` `QueuePatienceTimeConstant` | 10 min | `H = H₀·e^(−(w−5 min)/τ_q)` — 37 % left after 15 min |
| `G_max` `MaxGForce` (existing) | 4.5 g | intensity 100 reference and episode threshold |
| `k_h` `HappinessGainRate` | 1.5 pt/s | happiness gain rate at a perfect match |
| `σ_h` `HappinessMatchWidth` | 12 pt | Gaussian width: `rate = k_h·e^(−(Δ/σ_h)²)`, ≈0.2 % of peak at Δ = 30 |
| `NauseaExcessThreshold` | 30 pt | excess at which nausea starts growing |
| `r_n` `NauseaBaseRate` | 1 pt/s | growth rate at zero nausea |
| `λ_n` `NauseaGrowthRate` | 0.15 /s | self-reinforcement: `dN/dt = r_n + λ_n·N` |
| `MaxGEpisodeDuration` | 1 s | sustained-max-G window |
| `MaxGNauseaPenalty` | 25 pt | added once per episode |

- **Why a Gaussian for happiness rather than a linear tent?** "Significantly happier around the preference" calls for a sharp peak with smooth tails. The Gaussian is symmetric and has no kink to test around.
- **Why `r_n + λ_n·N` for nausea rather than `λ·N`?** Pure `λ·N` never leaves zero, and every guest starts at nausea 0.
- **The step uses the exact solution:** `N' = (N + r_n/λ_n)·e^(λ_n·dt) − r_n/λ_n`, then clamp. This matches the symplectic-integrator philosophy in `docs/02`. The result is exact for any `dt`, and tests can assert closed-form values.

### D2 — "Intensity" is horizontal felt G over `MaxGForce`

`intensity = min(100, 100·√(ForwardG² + LateralG²) / MaxGForce)`. The value is computed once per gondola per step in `Gondola.AdvancePhysics`, straight after `UpdateGForces`.

- *Alternative: rotation speed as a fraction of max angular velocity.* Rejected, because the request explicitly frames intensity in terms of max allowed G-forces.
- *Alternative: the larger of the two axes.* Rejected, because the vector magnitude is what the body feels.

### D3 — Queue erosion is computed at read time, not ticked

`QueuedGroup` gains `DateTimeOffset QueuedAt`, which `RideQueueService` stamps from `TimeProvider` when it enqueues. `Person` keeps its **arrival** happiness. A pure domain service `QueuePatience.HappinessAfter(double arrivalHappiness, TimeSpan waited)` computes the waited value whenever the service maps a group to a DTO: in `GetStatus` (per person, plus the average) and in `TakeGroupAsync` (the value that boards).

- *Alternative: a background ticker that rewrites `Person.Happiness`.* Rejected. It adds a timer and lock contention for nothing, the result depends on how often the ticker runs, and the closed form is exact anyway.
- `Person` stays immutable after construction. The three ratings are constructor arguments, validated against `MinRating = 0` / `MaxRating = 100` and rejected with `DomainValidationException`, matching the existing weight validation.

### D4 — A richer boarding contract inside DigitalTwin

- New value object `PassengerExperience(double Happiness, double PreferredIntensity, double Nausea)`, a validated record. It has `Clamp`-based `With…` helpers and the pure evolution functions `AfterRideStep(double intensity, double dtSeconds)` and `WithMaxGPenalty()`.
- New value object `BoardingPassenger(PassengerWeight Weight, PassengerExperience Experience)`.
- `Ride.BoardGroup` and `IRideStore.BoardGroup` take `IReadOnlyList<BoardingPassenger>` instead of `IReadOnlyList<PassengerWeight>`.
- `Passenger` gains `PassengerExperience Experience { get; private set; }`, mutated only through intent methods `ExperienceRideStep(intensity, dt)` and `SufferSustainedMaxG()`, which call `MarkChanged()` per ADR-0003.
- `RideLoadingCoordinator` maps `PersonDto` → `BoardingPassenger`.
- *Alternative: keep `PassengerWeight` and pass experience alongside.* Rejected, because two parallel lists invite index bugs.

### D5 — Gondola owns the max-G episode

`Gondola` gains `_maxGEpisodeElapsed` (TimeSpan) and `_maxGPenaltyApplied` (bool). On each step:

- If the felt magnitude is at least `MaxGForce`, the gondola adds `dt`. Once the elapsed time exceeds `MaxGEpisodeDuration` and the penalty has not been applied, it calls `SufferSustainedMaxG()` on each occupant and sets the flag.
- Otherwise it resets both.

The gondola then calls `ExperienceRideStep(intensity, dt)` on each occupant. `Seat` gets an internal `Occupant` accessor (or a `Seat.ApplyToOccupant(Action<Passenger>)`), so the occupant stays non-public.

Rider evolution only runs inside `AdvancePhysics`. As a result it is automatically limited to the Started/Stopping/EmergencyStop states, which is what the spec requires: there are no changes during Loading or Offloading.

### D6 — Aggregates on the wire

- **DigitalTwin.Abstractions:** add `RiderExperienceTelemetry(double? AverageHappiness, double? AveragePreferredIntensity, double? AverageNausea)` as a new trailing `RiderExperience` member of `RideTelemetry`. `Ride.ToTelemetry()` computes it in the same seat walk that already sums the load. All three values are `null` when no seat is occupied.
- **Queue.Abstractions:** `PersonDto` gains `Happiness`, `PreferredIntensity` and `Nausea`. `GetQueueStatusResponse` gains `double? AverageHappiness`. Run the `dto-organization` skill on the touched DTOs and fix any placement it flags.
- Both changes are additive positional-record extensions. Every construction site in tests must be updated, and the compiler finds them.

### D7 — Frontend: one presentational panel, PrimeNG `ProgressBar`

- Wire `providePrimeNG({ theme: { preset: Aura } })` in `app.config.ts`. Scope the theme with a CSS layer so the existing hand-rolled panels are not restyled. Check the `primeng` MCP server (`get_setup`, `get_component progressbar`, accessibility guide) before writing the code.
- `ride.models.ts`: add a `RiderExperience` type with `number | null` fields, and map the wire field. The frame mapper defaults to all-null when the field is missing, which keeps older backends safe.
- `queue.models.ts`: add `averageHappiness: number | null` to `QueueStatusDto`/`QueueStatus`.
- New `ride-dashboard/panels/rider-experience-panel/` component (OnPush, signal inputs `queueHappiness` and `riderExperience`). It renders four rows, each with:
  - a visible label,
  - a `p-progressbar` with `[value]` and `[showValue]="false"`,
  - adjacent visible numeric text, or "No data" when the value is null.
- Each bar gets an accessible name through `aria-labelledby` pointing at its label. The rows sit in an `aria-live="polite"` region, and the values are rounded to whole numbers so the live region does not chatter at 30 Hz.
- `ride-dashboard.html` places the panel in a `<div class="panel">` straight after the gondola panel. The dashboard binds `queueState.averageHappiness()` and `ride.riderExperience()`.
- *Alternative: native `<progress>`.* Rejected, because CLAUDE.md mandates PrimeNG for new UI.

## Risks / Trade-offs

- **[Risk] Happiness saturates at 100 within about a minute at a perfect match.** At 1.5 pt/s, a 75 → 100 climb takes about 17 s. → This is acceptable for a demo-scale ride cycle. The constants live in `RideParameters` and the docs chapter, so tuning them is a one-line change.
- **[Risk] Some rides may never reach 4.5 g**, which would leave the episode rule untested in practice. → Hub arm radius 2 m × (5 rad/s)² ≈ 5.1 g from the hub alone, so full power reaches it. Domain tests drive the gondola directly with synthetic G values either way.
- **[Risk] Contract churn overlaps the in-flight `single-riders-queue` change**, which also reshapes `PersonDto`/`GetQueueStatusResponse` and boarding. → Both changes are additive. Whichever lands second rebases, and the `BoardingPassenger` type is the natural carrier for single riders too.
- **[Trade-off] Duplicated rating bounds (0–100) in Queue and DigitalTwin.** → Deliberate: each bounded context owns its own domain types (ADR-0004). Sharing them via `Shared/Core` would put module-specific logic there.
- **[Risk] PrimeNG theme leaking into hand-rolled panels.** → Use a CSS layer order, and the axe/visual check in the integration task.
- **[Trade-off] Queue erosion is only visible at 5 s poll granularity.** That is fine for a value that changes over minutes.

## Migration Plan

The state is in-memory only, so there is no data migration. The wire changes are additive. The frontend tolerates a missing `riderExperience` / `averageHappiness` (they map to null), so the frontend and backend can be deployed in either order. Rollback is a plain revert.
