## Why

Guests are currently nothing more than a name and a body weight: the twin cannot say whether a ride programme is pleasant or sickening, or whether the line is so long that people are leaving grumpy. Giving every guest a ride-intensity preference, a happiness value and a nausea rating turns the simulation into a guest-experience model the operator can actually read on the dashboard, and gives future ride programmes (the `Controller` module) a target to optimise.

## What Changes

- Every generated **person** gets a **rider profile**: a preferred intensity in `[0.1, 1]`, a happiness in `[0, 1]` (drawn in `[0.65, 0.85]` on arrival) and a nausea rating in `[0, 1]` (starts at `0`).
- **Queue wait affects happiness.** Once a group has waited longer than five minutes, its members' happiness slowly decreases for as long as they keep waiting. The queue status reports each person's current happiness and the average happiness of everyone still in line.
- **Boarding carries the profile onto the ride.** A person who boards becomes a passenger with the same preferred intensity, their current (wait-adjusted) happiness and their nausea rating.
- **The ride intensity works on the riders.** While the ride is in motion, each gondola's felt G-force is expressed as an intensity in `[0, 1]` relative to the maximum allowed G. A rider whose preference matches the intensity gains happiness at `0.1/s`; a rider on a ride more intense than they prefer loses happiness at `0.1/s` and gains nausea at `0.2/s`.
- **Sustained maximum G is nauseating.** A gondola held at or above the maximum allowed G for two seconds or longer adds `0.5` nausea to every rider in it, once per such episode.
- **Telemetry** frames gain a ride-level rider-mood roll-up: rider count, average happiness and average nausea of the people currently on the ride.
- **Dashboard** gets a new **Rider mood** panel in the right rail, below the Gondolas panel, showing average queue happiness (people in the queue only), average rider happiness and average nausea (people on the ride only).
- Wire contracts change **additively**: `PersonDto`, `GetQueueStatusResponse` and `RideTelemetry` gain fields; nothing is removed.

## Capabilities

### New Capabilities
- `rider-profile`: the per-person preferred intensity, happiness and nausea values, how they are generated on arrival, how waiting in the queue erodes happiness, and how the queue status exposes them.
- `rider-experience`: how a passenger's happiness and nausea evolve on the ride from the gondola's felt G-force, the sustained-maximum-G penalty, and the rider-mood roll-up on the telemetry snapshot.
- `rider-mood-panel`: the dashboard panel that shows queue happiness, rider happiness and nausea.

### Modified Capabilities
- `ride-loading`: boarded members become passengers **carrying their rider profile** (preferred intensity, current happiness, nausea), not just their weight.
- `ride-telemetry-streaming`: the full telemetry payload additionally carries the rider-mood roll-up (rider count, average happiness, average nausea).

## Impact

- **Queue module** (`FourDotnet.BoogaBooster.Queue`, `.Abstractions`)
  - `Domain/Person.cs` gains a validated `RiderProfile` value object; `Domain/QueuedGroup.cs` gains its enqueue timestamp and wait-adjusted happiness; `Domain/RideQueue.cs` stamps arrivals with the current time and exposes the average happiness of the line.
  - `Filling/PersonGenerator.cs` draws the profile; `QueueModuleOptions.cs` gains the grumpiness onset and rate.
  - `DataTransferObjects/PersonDto.cs` and `DataTransferObjects/GetQueueStatus/GetQueueStatusResponse.cs` gain the new fields; `RideQueueService.cs` maps them.
  - `Shared/IntegrationMessages` `GroupQueuedIntegrationEvent` is **unchanged** (no personal mood data on the bus).
- **DigitalTwin module** (`FourDotnet.BoogaBooster.DigitalTwin`, `.Abstractions`)
  - `Domain/Passenger.cs` carries and evolves the profile; `Domain/Gondola.cs` derives intensity from its felt G, tracks time at the G limit and drives the riders' experience each physics tick; `Domain/RideParameters.cs` gains the rider-experience constants.
  - `Application/RideLoadingCoordinator.cs`, `IRideStore`/`RideStore` and `Ride.BoardGroup` pass the profile through; `IRideEventSampler` draws a profile for manually boarded passengers.
  - `Abstractions/RideTelemetry.cs` gains a `RiderMoodTelemetry` record; query handlers add span attributes; offload records happiness/nausea histograms (ADR-0009).
- **Angular app** (`FourDotnet.BoogaBooster.App`)
  - New `ride-dashboard/panels/rider-mood-panel` (PrimeNG-based, with an a11y spec); `ride.models.ts` and `queue.models.ts` map the new wire fields; `RideStateService` / `QueueStateService` expose them; `ride-dashboard.html` adds the panel below the Gondolas panel.
- **Docs**: new `docs/06-rider-experience.md` describing the intensity mapping and the mood dynamics, plus new rows in `docs/appendix-parameters.md` (no magic numbers in the physics).
- **Tests**: `Queue.Tests`, `DigitalTwin.Tests` (domain dynamics, boarding pass-through, telemetry) and Vitest specs for the new panel, models and state services. Coverage floor of 80 % on both module libraries maintained.
