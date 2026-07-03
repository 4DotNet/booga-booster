namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The live telemetry the central mill emits, including the load-balance reading
/// used to detect uneven loading across its four arms.
/// </summary>
/// <param name="PowerWatts">Electrical power the mill motor is consuming.</param>
/// <param name="Rpm">Current rotation speed in revolutions per minute.</param>
/// <param name="LoadKg">Total measured rotating load carried by the mill.</param>
/// <param name="PassengerLoadKg">Combined weight of every seated passenger across the ride.</param>
/// <param name="ImbalanceMillimeters">
/// How far the combined centre of mass sits off the spin axis. Zero when opposite
/// arms carry matching loads; it grows as loading becomes lopsided.
/// </param>
/// <param name="IsBalanced">
/// <c>false</c> when <paramref name="ImbalanceMillimeters"/> exceeds the safe
/// limit, in which case the ride is unsafe with reason "unbalanced load".
/// </param>
/// <param name="IsOverloaded">
/// <c>true</c> when <paramref name="PassengerLoadKg"/> exceeds the maximum safe
/// load, in which case the ride is unsafe with reason "overloaded".
/// </param>
public sealed record MillTelemetry(
    double PowerWatts,
    double Rpm,
    double LoadKg,
    double PassengerLoadKg,
    double ImbalanceMillimeters,
    bool IsBalanced,
    bool IsOverloaded);
