using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.BoardPassenger;

/// <summary>Boards a passenger into a specific seat of a specific gondola.</summary>
/// <param name="HubIndex">The hub (0–3) the gondola hangs from.</param>
/// <param name="GondolaIndex">The gondola's index (0–3) on the hub.</param>
/// <param name="Seat">Which seat to board.</param>
/// <param name="WeightKg">The passenger's weight; a random weight is used when <c>null</c>.</param>
public sealed record BoardPassengerCommand(
    int HubIndex,
    int GondolaIndex,
    SeatPosition Seat,
    double? WeightKg) : Command;
