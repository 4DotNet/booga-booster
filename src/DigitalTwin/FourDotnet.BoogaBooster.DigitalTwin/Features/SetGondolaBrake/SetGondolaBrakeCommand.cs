using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetGondolaBrake;

/// <summary>Engages or releases a specific gondola's yaw brake.</summary>
/// <param name="HubIndex">The hub (0–3) the gondola hangs from.</param>
/// <param name="GondolaIndex">The gondola's index (0–3) on the hub.</param>
/// <param name="Brake">The desired brake state.</param>
public sealed record SetGondolaBrakeCommand(
    int HubIndex,
    int GondolaIndex,
    GondolaBrakeState Brake) : Command;
