using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Holds the single in-memory <see cref="Ride"/> aggregate behind a thread-safe
/// boundary shared by the simulation loop (writer) and the command/query handlers.
/// Every operation returns a consistent telemetry snapshot of the resulting state.
/// The ride is ephemeral: it starts idle and empty and is not persisted.
/// </summary>
public interface IRideStore
{
    /// <summary>Returns a consistent snapshot of the ride's current telemetry.</summary>
    RideTelemetry GetTelemetry();

    /// <summary>Advances the simulation by one fixed <paramref name="dt"/> step.</summary>
    RideTelemetry Advance(TimeSpan dt);

    /// <summary>Sets the main (mill) engine power.</summary>
    RideTelemetry SetMainEnginePower(EnginePower power);

    /// <summary>Sets the hub engine power (applied identically to all four hubs).</summary>
    RideTelemetry SetHubEnginePower(EnginePower power);

    /// <summary>
    /// Boards a passenger into a seat. When <paramref name="weight"/> is
    /// <c>null</c> a random weight is drawn; the natural restraint-close delay is
    /// always drawn from the sampler.
    /// </summary>
    RideTelemetry BoardPassenger(int hubIndex, int gondolaIndex, SeatPosition seat, PassengerWeight? weight);

    /// <summary>Engages or releases a specific gondola's yaw brake.</summary>
    RideTelemetry SetGondolaBrake(int hubIndex, int gondolaIndex, GondolaBrakeState brake);

    /// <summary>Starts the ride, if it is safe to do so.</summary>
    RideTelemetry StartRide();

    /// <summary>Begins a controlled ramp down to a stop.</summary>
    RideTelemetry StopRide();
}
