<!-- github-issue: 9 -->
> GitHub issue: [#9 Passenger experience: happiness, preferred ride intensity and nausea, with a Rider Experience panel](https://github.com/4DotNet/booga-booster/issues/9)

## Why

Guests are only a weight today. Once seated, a passenger is a `PassengerWeight`, and nothing in the twin reflects whether the people in the queue or on the ride are having a good time. Giving each guest a mood, a preferred ride intensity and a nausea level lets the simulation show what operating choices mean for people: long waits, a ride that is too tame, or a ride that is too wild. It also gives the operator a direct read-out of guest experience next to the physics telemetry.

Source request: `docs/feature-requests/passenger-happiness.md`.

## What Changes

- Every person gets three experience attributes on a 0–100 scale (0 lowest, 100 highest):
  - **Happiness**: 0 is very sad, 100 is extremely happy.
  - **Preferred ride intensity**: 0 is very tame, 100 is the maximum allowed G-force.
  - **Nausea**: 0 is fine, 100 is extremely nauseous.
- Starting values when a person arrives in the queue:
  - Happiness is uniformly random in **65–85**.
  - Preferred intensity is uniformly random in **50–100**.
  - Nausea is **0**.
  - All draws use the seedable person generator, so runs stay deterministic.
- **Queue waiting.** The queue records when each group joined it. After a group has waited more than **5 minutes**, each member's happiness decays exponentially with the extra waiting time.
- **Boarding hand-off.** A guest's current values travel with them from the queue onto the ride. Today only their weight does.
- **Ride intensity.** A gondola's intensity is its felt G-force as a percentage of the maximum allowed G-force (`MaxGForce`, 4.5 g), clamped to 0–100.
- **Riding.** On every physics tick each seated passenger reacts to their gondola's intensity:
  - **Happiness goes up** when the intensity is close to the passenger's preference. The closer the match, the faster it rises. A 50-preference rider is happiest near 50 % of max G; a 100-preference rider is happiest when the ride touches max G.
  - **Nausea grows exponentially** while the intensity exceeds the passenger's preference by **30 points or more**.
  - **Over-G episodes add nausea.** When a gondola stays at or above max G for **more than 1 second**, each of its passengers gains **+25 nausea points**. This happens once per episode; a new episode needs the G-force to drop below max first.
- **Telemetry** frames carry a rider-experience summary: average happiness, average preferred intensity and average nausea of everyone on the ride. The fields are additive.
- **Queue status** carries the average happiness of everyone waiting, and each person's three attributes. The fields are additive.
- **Rider Experience panel**: a new panel in the right telemetry rail, directly under the Gondolas panel, with four progress bars:
  - Queue happiness
  - Rider happiness
  - Rider intensity
  - Rider nausea

## Capabilities

### New Capabilities
- `passenger-experience`: covers the three experience attributes and their ranges, the starting values on arrival, happiness decay while queueing, carrying the attributes through boarding, the ride-intensity measure, the on-ride happiness and nausea dynamics, and the over-G nausea episodes.
- `rider-experience-panel`: covers the dashboard panel that shows queue happiness, rider happiness, rider intensity and rider nausea, including its placement, empty states and accessibility.

### Modified Capabilities
- `ride-telemetry-streaming`: the "Full telemetry payload" requirement now includes the rider-experience summary (average happiness, preferred intensity and nausea of boarded passengers).

## Impact

- **Queue module**
  - `Domain/Person.cs` gains the attributes and a decay method.
  - `Domain/QueuedGroup.cs` and `Domain/RideQueue.cs` record the enqueue time.
  - `Filling/PersonGenerator.cs` draws the starting values.
  - `QueueModuleOptions` gains the decay settings.
  - The queue status query/handler computes current happiness and the queue average.
- **Queue.Abstractions**
  - `PersonDto` gains `Happiness`, `PreferredRideIntensity` and `Nausea`.
  - `GetQueueStatusResponse` gains `AverageHappiness`.
- **DigitalTwin module**
  - `Domain/Passenger.cs` gains an experience value object.
  - `Domain/Gondola.cs` measures intensity, times over-G episodes and updates its passengers each tick.
  - `Domain/RideParameters.cs` gets the new tuning constants.
  - `Application/RideLoadingCoordinator.cs` maps the full `PersonDto`, not just the weight.
- **DigitalTwin.Abstractions**: a new `RiderExperienceTelemetry` record on `RideTelemetry`.
- **Docs**: a new `docs/06-passenger-experience.md` with the formulas and constants, plus entries in `docs/appendix-parameters.md`.
- **Frontend**
  - New `panels/rider-experience-panel`, built with the PrimeNG ProgressBar.
  - Model mapping for the new telemetry and queue fields.
  - Wiring in `ride-dashboard.html`.
- **Tests**
  - `Queue.Tests`: generator ranges, decay curve, status average.
  - `DigitalTwin.Tests`: intensity, happiness gain, nausea growth, over-G episodes, boarding hand-off.
  - Vitest and axe specs for the panel.
- **Overlap**: the in-flight `passenger-safety` change also touches the `Ride.Advance` safety paths and the telemetry records. Both changes are additive, but they will need a merge if they are implemented at the same time.
