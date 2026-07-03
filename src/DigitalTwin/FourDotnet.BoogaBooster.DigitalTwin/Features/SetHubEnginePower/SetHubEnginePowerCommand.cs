using FourDotnet.BoogaBooster.Core.Cqrs;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEnginePower;

/// <summary>Sets the hub engine power. All four hubs receive the same setting.</summary>
/// <param name="Percent">The throttle setting (0–100).</param>
public sealed record SetHubEnginePowerCommand(double Percent) : Command;
