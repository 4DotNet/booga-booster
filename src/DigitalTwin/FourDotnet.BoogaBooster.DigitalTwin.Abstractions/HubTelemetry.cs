namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The live telemetry a motor-driven hub emits. All four hubs receive the same
/// commanded power, but each spins at its own speed because its load differs — a
/// heavier hub accelerates more slowly and settles at a lower speed.
/// </summary>
/// <param name="Index">The hub's index (0–3) on the mill.</param>
/// <param name="PowerWatts">Electrical power the hub motor is consuming.</param>
/// <param name="Rpm">Current rotation speed in revolutions per minute.</param>
/// <param name="LoadKg">Total measured passenger + gondola load carried by the hub.</param>
public sealed record HubTelemetry(
    int Index,
    double PowerWatts,
    double Rpm,
    double LoadKg);
