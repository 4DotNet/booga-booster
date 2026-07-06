using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetMainEngineDirection;

/// <summary>Sets the main (mill) engine rotation direction.</summary>
/// <param name="Direction">The commanded rotation direction (Forward or Reverse).</param>
public sealed record SetMainEngineDirectionCommand(MotorDirection Direction) : Command;
