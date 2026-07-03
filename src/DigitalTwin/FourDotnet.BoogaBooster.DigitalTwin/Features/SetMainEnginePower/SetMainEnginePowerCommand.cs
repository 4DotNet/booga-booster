using FourDotnet.BoogaBooster.Core.Cqrs;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetMainEnginePower;

/// <summary>Sets the main (mill) engine power.</summary>
/// <param name="Percent">The throttle setting (0–100).</param>
public sealed record SetMainEnginePowerCommand(double Percent) : Command;
