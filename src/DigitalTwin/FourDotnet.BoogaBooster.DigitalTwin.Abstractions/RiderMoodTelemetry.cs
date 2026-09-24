namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The rider-mood roll-up of one telemetry frame: how many passengers are seated on
/// the ride and, across them, how happy and how nauseous they are on average. It
/// covers passengers on the ride only — never the queue — and is computed on the
/// backend so a 30 Hz frame carries three numbers rather than thirty-two moods.
/// </summary>
/// <param name="RiderCount">
/// How many passengers are seated — the same number as the frame's boarded-passenger count.
/// </param>
/// <param name="AverageHappiness">
/// The mean happiness of the seated passengers in <c>[0, 1]</c>, or <c>null</c> when nobody is seated.
/// </param>
/// <param name="AverageNausea">
/// The mean nausea of the seated passengers in <c>[0, 1]</c>, or <c>null</c> when nobody is seated.
/// </param>
public sealed record RiderMoodTelemetry(
    int RiderCount,
    double? AverageHappiness,
    double? AverageNausea);
