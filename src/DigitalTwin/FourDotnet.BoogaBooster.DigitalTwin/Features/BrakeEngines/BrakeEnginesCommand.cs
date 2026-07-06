using FourDotnet.BoogaBooster.Core.Cqrs;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.BrakeEngines;

/// <summary>Applies the brakes to the drive engines: cuts power to the mill and every hub.</summary>
public sealed record BrakeEnginesCommand : Command;
