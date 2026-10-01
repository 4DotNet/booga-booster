namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The experience of the people currently on the ride, averaged over every occupied
/// seat, each on the 0–100 scale. All three averages are <c>null</c> — absent, not
/// zero — when nobody is on board.
/// </summary>
/// <param name="AverageHappiness">The riders' mean happiness (0 very sad, 100 extremely happy).</param>
/// <param name="AveragePreferredIntensity">The riders' mean preferred ride intensity (0 tamest, 100 most intense).</param>
/// <param name="AverageNausea">The riders' mean nausea (0 none, 100 maximum).</param>
public sealed record RiderExperienceTelemetry(
    double? AverageHappiness,
    double? AveragePreferredIntensity,
    double? AverageNausea)
{
    /// <summary>The summary of an empty ride: no averages.</summary>
    public static RiderExperienceTelemetry Empty { get; } = new(null, null, null);
}
