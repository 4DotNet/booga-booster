namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions.DataTransferObjects;

/// <summary>
/// How everyone on board is getting on, summarised for one telemetry frame
/// (docs/06 §6.7): the rider count and the average happiness, preferred ride
/// intensity and nausea, each on the 0–100 experience scale.
/// </summary>
/// <remarks>
/// Part of <see cref="RideTelemetry"/>, which both the telemetry query and the live
/// stream return, so it sits directly under <c>DataTransferObjects</c> rather than
/// inside one feature's namespace. Per-seat experience is deliberately not streamed.
/// </remarks>
/// <param name="RiderCount">How many passengers are on board.</param>
/// <param name="AverageHappiness">
/// The riders' average happiness, or <c>null</c> when nobody is on board — absent
/// rather than 0, so an empty ride is never mistaken for a miserable one.
/// </param>
/// <param name="AveragePreferredIntensity">The riders' average preferred ride intensity, or <c>null</c> when nobody is on board.</param>
/// <param name="AverageNausea">The riders' average nausea, or <c>null</c> when nobody is on board.</param>
public sealed record RiderExperienceTelemetry(
    int RiderCount,
    double? AverageHappiness,
    double? AveragePreferredIntensity,
    double? AverageNausea);
