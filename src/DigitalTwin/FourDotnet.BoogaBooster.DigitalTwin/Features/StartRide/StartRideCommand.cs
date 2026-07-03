using FourDotnet.BoogaBooster.Core.Cqrs;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.StartRide;

/// <summary>Starts the ride, if every safety interlock is satisfied.</summary>
public sealed record StartRideCommand : Command;
