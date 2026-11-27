# Proposal

## Why

Right now a guest is just a name and a weight. The twin can tell an operator whether the ride is *safe*, but not whether anyone is *enjoying* it. Giving every guest a mood makes two costs visible on the dashboard: a long queue and a ride programme that is too tame or too aggressive. The mood is how happy they are, how intense they want the ride to be, and how nauseous they are getting. With it, the operator can tune the ride for guest experience as well as for safety.

## What Changes

- Every person gets three scores on a 0–100 scale:
  - **happiness**: 0 is very sad, 100 is extremely happy.
  - **preferred ride intensity**: 0 is the gentlest ride, 100 the most intense.
  - **nausea**: 0 is fine, 100 is extremely nauseous.
- When a guest joins the queue, **happiness** is drawn uniformly from **65–85** and **preferred intensity** from **50–100**. **Nausea** starts at **0**. The draws use the existing seeded person generator, so runs stay deterministic.
- **Queue patience.** After a guest has waited **5 minutes**, their happiness starts to drop, and it drops faster the longer they wait (exponential decline).
- **Mood boards with the guest.** When a group leaves the queue, each member's happiness, preferred intensity and nausea go onto the ride with them.
- **Ride intensity** is measured per gondola as the felt G-force divided by the maximum allowed G-force (`G_max` = 4.5 g), expressed as a percentage.
- **Riding happiness.** While the ride runs, a rider becomes much happier when their gondola's intensity is close to their preferred intensity. A rider who prefers 50 % is happiest at about 50 % of `G_max`. A rider who prefers 100 % is happiest when the ride reaches `G_max`. A gentle rider is therefore happiest on a slow ride, and a thrill-seeker on a fast one.
- **Riding nausea.** When a gondola's intensity is **30 percentage points or more** above a rider's preference, that rider's nausea grows exponentially.
- **Over-G shock.** Each time a gondola stays at or above `G_max` for more than **1 second**, every passenger in it gains **25** nausea points.
- The **queue status** response gains the average happiness of everyone waiting, and each person in it carries their current mood. Both changes are additive.
- **Ride telemetry** (REST and SSE) gains a rider-experience summary: the rider count and the average happiness, preferred intensity and nausea of everyone on the ride. This change is additive.
- A new **Rider Experience** panel goes in the dashboard's right column, under the Gondolas panel. It has four labelled progress bars: **Queue happiness**, **Rider happiness**, **Rider intensity** and **Rider nausea**. It is built with PrimeNG and passes AXE / WCAG AA.

## Capabilities

### New Capabilities
- `guest-mood`: a guest's three mood scores. Covers how they are set on arrival, how waiting lowers happiness, the queue-wide average happiness on the queue status, and how the mood goes onto the ride when a group boards.
- `rider-experience`: how a passenger's mood changes while riding. Covers per-gondola intensity, happiness from a matching intensity, exponential nausea from too much intensity, the over-G nausea shock, when mood updates run, and the ride-wide rider-experience summary.

### Modified Capabilities
- `ride-telemetry-streaming`: the *Full telemetry payload* requirement adds the rider-experience summary to every frame.
- `ride-telemetry-panels`: adds the Rider Experience panel to the telemetry rail, under the gondola panel.

## Impact

- **Backend, `Queue` module.**
  - `Domain/Person` gains the mood scores and the queue-wait decline.
  - `Domain/QueuedGroup` and `Domain/RideQueue` record when a group joined.
  - `Filling/PersonGenerator` draws the initial mood.
  - `RideQueueService` maps each person's current mood into the DTOs and computes the average.
- **Backend, `Queue.Abstractions`.** `PersonDto` gains `Happiness`, `PreferredRideIntensity` and `Nausea`. `GetQueueStatusResponse` gains `AverageHappiness`.
- **Backend, `DigitalTwin` module.**
  - A new `PassengerProfile` value object replaces the bare weight when a group boards.
  - `Domain/Passenger` carries and updates the mood, and `Domain/Seat` passes updates through to it.
  - `Domain/Gondola` computes intensity and tracks over-G episodes.
  - `Domain/Ride` builds the summary.
  - `Application/RideLoadingCoordinator` maps the queue mood onto the profile.
  - Every new constant goes in `Domain/RideParameters`.
- **Backend, `DigitalTwin.Abstractions`.** `RideTelemetry` gains a `RiderExperienceTelemetry` record.
- **Docs.** A new `docs/06-rider-experience.md` derives the mood model, and `docs/appendix-parameters.md` lists its constants. They are the specification for the code and its tests.
- **Frontend (`FourDotnet.BoogaBooster.App`).**
  - The queue and ride models and state services map the new fields.
  - A new `rider-experience-panel` component is wired into `ride-dashboard.html`. It is the app's first PrimeNG component, so the PrimeNG theme provider is set up in `app.config.ts`.
  - Vitest and axe specs cover it.
- **No new dependencies, integration events or endpoints.** The HTTP/SSE contract changes are additive only, so they go through `api-contract-guardian`.
