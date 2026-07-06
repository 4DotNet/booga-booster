using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.RequestRideStateTransition;

/// <summary>
/// Requests an operator-triggered lifecycle transition to <paramref name="Target"/>.
/// The domain state machine rejects an illegal or guard-failing transition.
/// </summary>
/// <param name="Target">The state the operator wants the ride to move to.</param>
public sealed record RequestRideStateTransitionCommand(RideState Target) : Command;
