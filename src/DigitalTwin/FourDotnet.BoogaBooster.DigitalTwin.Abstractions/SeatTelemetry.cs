namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The live reading of a single seat's sensors: which seat it is, how much weight
/// its load cell measures, and what its restraint lock sensor reports.
/// </summary>
/// <param name="Position">Which seat this is.</param>
/// <param name="OccupiedKg">Weight measured by the seat's load cell (0 when empty).</param>
/// <param name="Restraint">The restraint lock sensor reading.</param>
public sealed record SeatTelemetry(
    SeatPosition Position,
    double OccupiedKg,
    RestraintState Restraint);
