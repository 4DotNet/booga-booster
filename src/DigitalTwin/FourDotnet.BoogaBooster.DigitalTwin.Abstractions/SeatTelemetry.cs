namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The live reading of a single seat's sensors: which seat it is, how much weight
/// its load cell measures, and what its restraint lock sensor reports.
/// </summary>
/// <param name="Position">Which seat this is.</param>
/// <param name="OccupiedKg">Weight measured by the seat's load cell (0 when empty).</param>
/// <param name="Restraint">The restraint lock sensor reading.</param>
/// <param name="IsOccupied">
/// <c>true</c> when a passenger is boarded in this seat. Distinguishes a genuinely
/// empty seat from an occupied one whose measured weight happens to be zero, so
/// consumers never have to infer occupancy from <paramref name="OccupiedKg"/>.
/// </param>
/// <param name="IsSecured">
/// <c>true</c> when the seat is occupied and its restraint has locked
/// (<see cref="RestraintState.Secured"/>). <c>false</c> while an occupant is still
/// securing their restraint during the loading countdown.
/// </param>
/// <param name="GuestNumber">
/// The seated rider's guest number from the queue; <c>null</c> when the seat is empty
/// or the rider was boarded by hand.
/// </param>
/// <param name="Happiness">The seated rider's happiness in [0, 100]; <c>null</c> when empty.</param>
/// <param name="PreferredG">The felt G (in g) the seated rider considers fun; <c>null</c> when empty.</param>
/// <param name="Nausea">The seated rider's nausea in [0, 100]; <c>null</c> when empty.</param>
public sealed record SeatTelemetry(
    SeatPosition Position,
    double OccupiedKg,
    RestraintState Restraint,
    bool IsOccupied,
    bool IsSecured,
    long? GuestNumber,
    double? Happiness,
    double? PreferredG,
    double? Nausea);
