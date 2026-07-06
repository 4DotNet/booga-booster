using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEngineDirection;

/// <summary>Sets the hub engine rotation direction. All four hubs receive the same setting.</summary>
/// <param name="Direction">The commanded rotation direction (Forward or Reverse).</param>
public sealed record SetHubEngineDirectionCommand(MotorDirection Direction) : Command;
