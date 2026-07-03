using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.StopRide;

/// <summary>Ramps the ride down through the store.</summary>
public sealed class StopRideCommandHandler : CommandHandler<StopRideCommand>
{
    private readonly IRideStore _store;

    public StopRideCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public override Task HandleAsync(StopRideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.StopRide();
        return Task.CompletedTask;
    }
}
