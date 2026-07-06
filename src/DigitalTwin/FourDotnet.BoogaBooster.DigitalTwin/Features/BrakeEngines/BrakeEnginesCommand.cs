using FourDotnet.BoogaBooster.Core.Cqrs;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.BrakeEngines;

/// <summary>
/// Sets the engine brake on the mill and every hub. When <paramref name="Engaged"/>
/// is <c>true</c> the drive power is cut and a strong braking torque is applied;
/// when <c>false</c> the brake is released.
/// </summary>
public sealed record BrakeEnginesCommand(bool Engaged) : Command;
