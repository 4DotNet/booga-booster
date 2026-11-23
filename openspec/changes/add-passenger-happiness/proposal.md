# Proposal

## Why

Today a guest is just a name and a weight: the twin can tell an operator whether the ride is *safe*, but not whether anyone is *enjoying* it. Giving every guest a mood — how happy they are, how intense they like their ride, and how nauseous they are getting — turns the twin into something an operator can tune for guest experience, and makes the cost of a long queue or an over-aggressive ride programme visible on the dashboard.

## What Changes

- Every person gets three mood attributes on a 0–100 scale: **happiness** (0 = very sad, 100 = extremely happy), **preferred ride intensity** (0 = gentlest, 100 = most intense) and **nausea** (0 = fine, 100 = extremely nauseous).
- A newly generated guest starts with happiness drawn uniformly from **65–85**, preferred intensity drawn uniformly from **50–100**, and nausea **0**. Draws go through the seeded generator so tests stay deterministic.
- **Queue patience:** a guest who has waited longer than **5 minutes** loses happiness exponentially with every further minute in line.
- **Mood boards with the guest:** the happiness, preference and nausea a guest has when their group leaves the queue are carried onto the ride.
- **Ride intensity** is defined per gondola as the felt G-force as a percentage of the maximum allowed G-force (`G_max`, 4.5 g).
- **Riding happiness:** while the ride runs, a rider gets markedly happier when their gondola's intensity is close to their preferred intensity. A 50 % rider peaks around 50 % of `G_max`; a 100 % rider peaks when the ride touches `G_max`.
- **Riding nausea:** when a gondola's intensity exceeds a rider's preference by **30 percentage points or more**, that rider's nausea grows exponentially.
- **Over-G shock:** each time a gondola stays at or above `G_max` for longer than **1 second**, every passenger in it gains **25** nausea.
- The **queue status** response reports the average happiness of everyone waiting. Each queued person's DTO carries their current mood. Both changes are additive.
- **Ride telemetry** (REST and SSE) gains a rider-experience summary: rider count, plus average happiness, average preferred intensity and average nausea across everyone on the ride. Additive.
- A new **Rider Experience** panel sits in the dashboard's right rail, under the Gondolas panel. It has four labelled progress bars: queue happiness, rider happiness, rider intensity and rider nausea. It is built with PrimeNG and is WCAG AA compliant.

## Capabilities

### New Capabilities
- `guest-mood`: the three mood attributes of a queued guest, how they are initialised on arrival, how queue waiting erodes happiness, and the queue-wide average happiness exposed on the queue status.
- `rider-experience`: how a boarded passenger's mood evolves on the ride. Covers the per-gondola intensity measure, happiness gain from a matching intensity, exponential nausea from over-intensity, the over-G nausea shock, and the rider-experience summary on ride telemetry.
- `rider-experience-panel`: the operator dashboard panel that visualises queue and rider mood as four progress bars.

### Modified Capabilities
<!-- None. Boarding, queue and telemetry contracts gain fields additively; no existing requirement changes. -->

## Impact

- **Backend: `Queue` module.** `Domain/Person.cs` gains the mood attributes and the queue-wait decay. `Domain/QueuedGroup.cs` and `Domain/RideQueue.cs` record when a group joined. `Filling/PersonGenerator.cs` randomises the initial mood. `RideQueueService` maps the current mood into the DTOs and averages it.
- **Backend: `Queue.Abstractions`.** `PersonDto` gains `Happiness`, `PreferredRideIntensity` and `Nausea`. `GetQueueStatusResponse` gains `AverageHappiness`.
- **Backend: `DigitalTwin` module.** `Domain/Passenger.cs` carries and evolves the mood through a new passenger-profile value object that replaces the bare weight at boarding. `Domain/Gondola.cs` computes intensity and tracks over-G episodes. `Domain/Ride.cs` and the boarding flow carry the profile from the queue DTO. `Domain/RideParameters.cs` holds every new constant.
- **Backend: `DigitalTwin.Abstractions`.** New `RiderExperienceTelemetry` record on `RideTelemetry`.
- **Docs.** New `docs/06-rider-experience.md` derives the mood model, and `docs/appendix-parameters.md` gains its constants. Both are the specification the domain code and tests are written against.
- **Frontend (`FourDotnet.BoogaBooster.App`).** Queue and ride models and state services map the new fields. A new `rider-experience-panel` component, wired into `ride-dashboard.html`. Vitest and axe specs.
- **Tests.** `Queue.Tests`, `DigitalTwin.Tests`, and the Angular specs. The ≥ 80 % coverage floor holds.
- No breaking API changes and no new packages. PrimeNG is already installed; this is the first component to use it.
