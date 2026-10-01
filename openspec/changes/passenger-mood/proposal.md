## Why

Guests are currently just weights: they queue, board and vanish without any experience of the ride. Giving every guest a mood — happiness, a personal "fun" G-force and nausea — turns the twin into something an operator can *read* (is the queue getting angry? is the ride too wild for this crowd?) and something visitors can *see*, with a RollerCoaster Tycoon-style park around the rig. Source request: `docs/feature-requests/passenger-happiness.md`, refined in an explore session.

## What Changes

- Every guest gets three mood values, all clamped to `[0, 100]` except preferred G:
  - **Happiness** — starts uniformly random in `[65, 85]`.
  - **Preferred G** — the felt G-force (in g) the guest considers fun, uniformly random between half the ride's safe limit and the safe limit (`RideParameters.MaxGForce`, 4.5 g → `[2.25, 4.5]`).
  - **Nausea** — starts at `0`.
- **Queue wait**: after 5 minutes in the queue a guest's happiness drops exponentially with the extra waiting time. Computed from the group's enqueue time on every read — no new background loop.
- **On the ride**: each gondola computes its **felt G** (`docs/05 §5.1`, gravity included). Riders get happier the closer felt G is to their preferred G, and get nauseous (exponentially) when felt G exceeds their preference by more than 30 % of the safe limit. Every continuous stretch of more than 1 s at or above the safe limit adds +25 nausea to that gondola's riders, once per stretch.
- **Riders keep their identity** from queue to seat; offloading records who left and how they felt.
- **Rider Experience panel** in the right rail under the Gondolas panel: four progress bars (queue happiness, rider happiness, rider preferred G, rider nausea) plus text counts of mad guests, sick riders and riders who left sick.
- **Park scene** in the central 3D visualization: entry and exit booths, a switchback queue showing the first 60 guests (with a `+N` overflow label), seated riders in the gondolas, and guests walking out of the exit. Guests are instanced pill bodies with a sphere head: LEGO yellow normally, **red when mad** (happiness < 30), **green when sick** (nausea ≥ 70, green wins). A sick guest leaving the exit leaves a puke puddle that dries up — a purely visual effect.

## Capabilities

### New Capabilities
- `passenger-mood`: the mood model — initial values, queue-wait decay, felt G, on-ride happiness/nausea rules, the max-G nausea penalty, and identity carried from queue to seat to exit.
- `rider-experience-panel`: the right-rail panel with the four averaged bars and the accessible mad/sick counts.
- `park-scene`: entry/exit booths, the visible queue with overflow, seated riders, exit walkers, mood head colours and the puke effect in the central visualization.

### Modified Capabilities
- `ride-queue`: queue status exposes each queued guest's current mood (additive).
- `ride-telemetry-streaming`: seat telemetry carries the seated rider's identity and mood, and telemetry reports the most recent offload (additive).

## Impact

- **Queue module** — `Person` gains mood values; `QueuedGroup` gains an enqueue timestamp; `PersonGenerator` draws the new random values from its seeded RNG; `RideQueueService` applies the wait decay when mapping to DTOs. `PersonDto` gains fields and moves to the mandated `DataTransferObjects/` layout while it is being touched.
- **DigitalTwin module** — `Passenger` carries identity and mood; `IRideStore.BoardGroup`/`Ride.BoardGroup` take passengers instead of bare weights; `Gondola` computes felt G and applies mood rules per tick; `Ride` records the last offload. New constants in `RideParameters.cs`, rationale in `docs/`.
- **Abstractions / telemetry** — `SeatTelemetry` gains rider fields; `RideTelemetry` gains `LastOffload`. All additive, no removals.
- **Frontend** — queue models map the group list; new `rider-experience-panel` (PrimeNG `ProgressBar`); `ride-visualization.ts` gains the park scene. 
- **Independent of `passenger-safety`**: that change trips on rpm; this one only *reads* `MaxGForce` as the comfort ceiling. If the rpm trip keeps the ride below 4.5 g, the max-G penalty simply never fires — acceptable, and verified by a spike task.
