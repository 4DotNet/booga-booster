# Proposal

## Why

Today a guest is only a name and a weight. The twin can tell an operator whether the ride is *safe*, but not whether anyone is *enjoying* it. If every guest has a mood (how happy they are, how intense they like the ride, how nauseous they are getting), the operator can see the cost of a long queue or a too-aggressive ride programme on the dashboard and tune the ride for guest experience, not only for safety.

## What Changes

- Every person gets three scores on a 0–100 scale: **happiness** (0 = very sad, 100 = extremely happy), **preferred ride intensity** (0 = gentlest, 100 = most intense) and **nausea** (0 = fine, 100 = extremely nauseous).
- When a guest joins the queue, their happiness is drawn uniformly from **65–85** and their preferred intensity from **50–100**. Nausea starts at **0**. The draws go through the existing seeded person generator, so runs stay deterministic.
- **Queue patience:** once a guest has waited **5 minutes**, their happiness starts to fall, and it falls faster the longer they wait (exponential decline).
- **Mood boards with the guest:** the happiness, preferred intensity and nausea a guest has when their group leaves the queue go with them onto the ride.
- **Ride intensity** is measured per gondola: the felt G-force as a percentage of the maximum allowed G-force (`G_max` = 4.5 g).
- **Riding happiness:** while the ride runs, a rider gets markedly happier when their gondola's intensity is close to their preferred intensity. A rider who prefers 50 % is happiest at about 50 % of `G_max`. A rider who prefers 100 % is happiest when the ride touches `G_max`.
- **Riding nausea:** when a gondola's intensity is **30 percentage points or more** above a rider's preference, that rider's nausea grows exponentially.
- **Over-G shock:** each time a gondola stays at or above `G_max` for longer than **1 second**, every passenger in it gains **25** nausea.
- The **queue status** response adds the average happiness of everyone waiting, and each person in it carries their current mood. Both changes are additive.
- **Ride telemetry** (REST and SSE) adds a rider-experience summary: the rider count and the average happiness, preferred intensity and nausea of everyone on the ride. This is additive.
- A new **Rider Experience** panel goes in the dashboard's right rail, under the Gondolas panel. It has four labelled progress bars: queue happiness, rider happiness, rider intensity and rider nausea. It is built with PrimeNG and passes AXE / WCAG AA.

## Capabilities

### New Capabilities
- `guest-mood`: a guest's three mood scores, how they are set on arrival, how waiting in the queue lowers happiness, the queue-wide average happiness on the queue status, and how mood is carried onto the ride when a group boards.
- `rider-experience`: how a passenger's mood changes on the ride. It covers per-gondola intensity, the happiness gained from a matching intensity, the exponential nausea from too much intensity, the over-G nausea shock, when mood updates run, and the ride-wide rider-experience summary.

### Modified Capabilities
- `ride-telemetry-streaming`: the *Full telemetry payload* requirement adds the rider-experience summary to every frame.
- `ride-telemetry-panels`: adds the Rider Experience panel to the telemetry rail, under the gondola panel.

## Impact

- **Backend, `Queue` module.** `Domain/Person` gains the mood scores and the queue-wait decline. `Domain/QueuedGroup` and `Domain/RideQueue` record when a group joined. `Filling/PersonGenerator` draws the initial mood. `RideQueueService` maps each person's current mood into the DTOs and computes the average.
- **Backend, `Queue.Abstractions`.** `PersonDto` gains `Happiness`, `PreferredRideIntensity` and `Nausea`. `GetQueueStatusResponse` gains `AverageHappiness`.
- **Backend, `DigitalTwin` module.** A new passenger-profile value object replaces the bare weight when a group boards. `Domain/Passenger` carries and updates the mood. `Domain/Gondola` computes intensity and tracks over-G episodes. `Domain/Ride` drives the updates and builds the summary. `Application/RideLoadingCoordinator` maps the queue mood onto the profile. Every new constant goes in `Domain/RideParameters`.
- **Backend, `DigitalTwin.Abstractions`.** `RideTelemetry` gains a new `RiderExperienceTelemetry` record.
- **Docs.** A new `docs/06-rider-experience.md` derives the mood model, and `docs/appendix-parameters.md` lists its constants. Both are the specification that the code and tests follow.
- **Frontend (`FourDotnet.BoogaBooster.App`).** The queue and ride models and state services map the new fields. A new `rider-experience-panel` component is wired into `ride-dashboard.html`. This is the app's first PrimeNG component, so the PrimeNG theme provider is set up in `app.config.ts`. Vitest and axe specs cover it.
- **No new dependencies, events or endpoints.** The HTTP contract changes are additive only.
