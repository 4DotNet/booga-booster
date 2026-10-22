namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// A ride-wide summary of how the riders are feeling: how many are aboard and the
/// average of their mood scores, each on a 0–100 scale. There is no per-seat mood on
/// the wire.
/// </summary>
/// <param name="RiderCount">How many passengers are seated on the ride.</param>
/// <param name="AverageHappiness">
/// Average happiness of everyone aboard, or <c>null</c> when nobody is aboard.
/// </param>
/// <param name="AveragePreferredRideIntensity">
/// Average preferred ride intensity of everyone aboard, or <c>null</c> when nobody is aboard.
/// </param>
/// <param name="AverageNausea">
/// Average nausea of everyone aboard, or <c>null</c> when nobody is aboard.
/// </param>
public sealed record RiderExperienceTelemetry(
    int RiderCount,
    double? AverageHappiness,
    double? AveragePreferredRideIntensity,
    double? AverageNausea);
