using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.StartRide;

/// <summary>Starts the ride through the store; the domain rejects an unsafe start.</summary>
public sealed class StartRideCommandHandler : CommandHandler<StartRideCommand>
{
    private readonly IRideStore _store;

    public StartRideCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(StartRideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.StartRide();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The command carries no payload, so the span records the state its work depended
    /// on — the lifecycle state as it found the ride. That is what separates a start
    /// refused from Idle from one refused from Started (design D2).
    /// </summary>
    protected override void EnrichActivity(Activity activity, StartRideCommand command)
        => activity.SetTag(RideTelemetryAttributes.State, _store.CurrentState.ToString());
}
