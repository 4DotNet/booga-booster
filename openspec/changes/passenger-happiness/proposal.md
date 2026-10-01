# Proposal

## Why

The twin models guests as nothing more than a body weight: a queued person has a number, a name and a weight, and once boarded only the weight survives. There is no way to see whether the ride is being operated *well* for the people on it — whether the queue is too long, whether the ride is too tame for thrill-seekers or too violent for the cautious. Giving every guest a happiness, a preferred ride intensity and a nausea rating turns the twin into something an operator can tune against, and gives the dashboard a guest-centred view next to the mechanical one. The request is captured in `docs/feature-requests/passenger-happiness.md`.

## What Changes

- Every guest gains three experience ratings, each a value on a **0–100** scale: **happiness** (0 very sad, 100 extremely happy), **preferred ride intensity** (0 tamest, 100 most intense) and **nausea** (0 none, 100 maximum).
- A guest entering the queue starts with a random happiness in **[65, 85]**, a random preferred intensity in **[50, 100]**, and nausea **0**. Randomness goes through the existing seedable person generator, so runs stay reproducible.
- The queue records **when each group joined**. Up to **5 minutes** of waiting is free; past that, a waiting guest's happiness **decays exponentially** with the extra waiting time.
- A guest's experience ratings **follow them onto the ride**: boarding carries happiness (after any queue decay), preferred intensity and nausea into the seated passenger instead of only the weight.
- While the ride runs, each rider's gondola has an **experienced intensity** — the gondola's felt G-force as a percentage of the maximum allowed G-force (4.5 g). Riders get **significantly happier** the closer that intensity is to their preference (a 50 % rider peaks around 50 % of max G; a 100 % rider peaks at max G).
- When the experienced intensity exceeds a rider's preference by **30 points or more**, the rider's nausea **grows exponentially** for as long as that persists.
- Each time a gondola stays at or above the maximum allowed G-force for **longer than 1 second**, both its riders gain **+25 nausea** (once per such episode), capped at 100.
- The queue status (`GET /rides/{rideId}/queue`) gains the **average happiness** of the people waiting; each queued person carries their ratings.
- The telemetry frame gains a **rider experience** summary: average happiness, average preferred intensity and average nausea of the people on the ride.
- The dashboard gains a **Rider Experience panel** in the right column, directly under the Gondolas panel, with four progress bars: **Queue happiness**, **Rider happiness**, **Rider intensity** and **Rider nausea**. It is the first panel built with PrimeNG.
- The physics of happiness and nausea is documented in a new `docs/` chapter, and its constants live in `RideParameters` (ride side) and the Queue module's domain constants (queue side) — no magic numbers.

## Capabilities

### New Capabilities

- `passenger-experience`: The per-guest happiness, preferred-intensity and nausea ratings — their range, how they are initialised on arrival, how waiting in the queue erodes happiness, how they are carried from the queue onto the ride, and how ride intensity and sustained maximum G-force change happiness and nausea for riders.

### Modified Capabilities

- `ride-queue`: the queue records when each group joined, and its status reports each waiting person's experience ratings plus the average happiness of everyone waiting.
- `ride-telemetry-streaming`: each telemetry frame carries the rider experience summary (average happiness, preferred intensity and nausea of the people on the ride).
- `ride-telemetry-panels`: the telemetry rail gains the Rider Experience panel with its four progress bars, placed directly under the gondola panel.

## Impact

- **Queue module** (`src/Queue/FourDotnet.BoogaBooster.Queue`): `Person` gains the three ratings with validated setters; `QueuedGroup` gains its queued-at time; a pure queue-patience policy computes waited happiness; `PersonGenerator` draws the initial ratings; `RideQueueService` stamps the join time and reports waited happiness and the queue average.
- **Queue abstractions**: `PersonDto` gains `Happiness`, `PreferredIntensity`, `Nausea`; `GetQueueStatusResponse` gains `AverageHappiness`. Additive, but a changed contract for the DigitalTwin loading coordinator and the Angular queue client.
- **DigitalTwin module** (`src/DigitalTwin/FourDotnet.BoogaBooster.DigitalTwin`): `Passenger` gains its experience state; boarding takes a richer passenger description than `PassengerWeight`; `Gondola` tracks its sustained-max-G episode and updates its riders every physics step; `Ride` aggregates rider experience into telemetry; new constants in `RideParameters`.
- **DigitalTwin abstractions**: `RideTelemetry` gains a `RiderExperience` record (additive).
- **Angular app**: queue models/source carry `averageHappiness`; ride models map the rider experience summary; new `rider-experience-panel` under `ride-dashboard/panels/`, using PrimeNG `ProgressBar` (which needs `providePrimeNG` wiring in `app.config.ts`); `ride-dashboard.html` places it under the gondola panel.
- **Docs**: new `docs/06-passenger-experience.md`; `docs/appendix-parameters.md` gains the experience constants.
- **Tests**: xUnit coverage for the ratings, generator ranges, queue decay, boarding carry-over, intensity/happiness/nausea evolution and the max-G episode rule; Vitest + axe specs for the panel and the model mapping.
- **Overlap with in-flight changes**: `single-riders-queue` also reshapes `PersonDto`/`GetQueueStatusResponse` and boarding; whichever lands second rebases onto the other. `passenger-safety` touches telemetry records additively and does not conflict.
- **Out of scope**: guests leaving the queue when unhappy, nausea feeding back into happiness, persistence of guest history after offloading, and any automatic over-G intervention (the max-G value is used here only as the intensity reference).
