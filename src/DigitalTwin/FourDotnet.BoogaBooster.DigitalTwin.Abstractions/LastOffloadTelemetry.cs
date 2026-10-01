namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The ride's most recent offload: who left and how they felt. A snapshot, not an
/// event — consumers detect a new offload by <paramref name="Counter"/> changing.
/// </summary>
/// <param name="Counter">
/// How many offloads the ride has completed; increases by one per offload. 0 before
/// the first offload.
/// </param>
/// <param name="Riders">The riders who left in that offload; empty before the first offload.</param>
public sealed record LastOffloadTelemetry(int Counter, IReadOnlyList<OffloadedRiderTelemetry> Riders)
{
    /// <summary>The state before the ride has ever offloaded.</summary>
    public static LastOffloadTelemetry None { get; } = new(0, []);
}
