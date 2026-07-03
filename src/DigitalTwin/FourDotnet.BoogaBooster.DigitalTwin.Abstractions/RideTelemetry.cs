namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// An immutable snapshot of the whole ride at one instant: the mill, the four
/// hubs, the sixteen gondolas, and the overall safety verdict. Emitted at a fixed
/// telemetry rate while the simulation runs.
/// </summary>
/// <param name="State">The ride's lifecycle state.</param>
/// <param name="SimulationTimeSeconds">Simulated time elapsed since the twin started.</param>
/// <param name="IsSafeToStart"><c>true</c> when the ride may be started right now.</param>
/// <param name="SafetyReason">Why the ride is (not) safe to start.</param>
/// <param name="Mill">The central mill's telemetry.</param>
/// <param name="Hubs">The four hubs' telemetry.</param>
/// <param name="Gondolas">The sixteen gondolas' telemetry.</param>
public sealed record RideTelemetry(
    RideState State,
    double SimulationTimeSeconds,
    bool IsSafeToStart,
    RideSafetyReason SafetyReason,
    MillTelemetry Mill,
    IReadOnlyList<HubTelemetry> Hubs,
    IReadOnlyList<GondolaTelemetry> Gondolas);
