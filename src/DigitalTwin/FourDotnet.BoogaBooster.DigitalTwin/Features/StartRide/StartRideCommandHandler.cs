using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.StartRide;

/// <summary>Starts the ride through the store; the domain rejects an unsafe start.</summary>
public sealed class StartRideCommandHandler : CommandHandler<StartRideCommand>
{
    private readonly IRideStore _store;

    public StartRideCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public override Task HandleAsync(StartRideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.StartRide();
        return Task.CompletedTask;
    }
}
