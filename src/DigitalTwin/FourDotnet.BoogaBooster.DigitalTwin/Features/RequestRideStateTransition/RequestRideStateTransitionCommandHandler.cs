using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.RequestRideStateTransition;

/// <summary>Requests a lifecycle transition through the store; the domain rejects an illegal one.</summary>
public sealed class RequestRideStateTransitionCommandHandler : CommandHandler<RequestRideStateTransitionCommand>
{
    private readonly IRideStore _store;

    public RequestRideStateTransitionCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(RequestRideStateTransitionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.RequestStateTransition(command.Target);
        return Task.CompletedTask;
    }
}
