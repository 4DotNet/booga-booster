using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.StopRide;

/// <summary>Ramps the ride down through the store.</summary>
public sealed class StopRideCommandHandler : CommandHandler<StopRideCommand>
{
    private readonly IRideStore _store;

    public StopRideCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(StopRideCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.StopRide();
        return Task.CompletedTask;
    }

    /// <summary>
    /// The command carries no payload, so the span records the lifecycle state as it
    /// found the ride — the state a refused stop was refused from (design D2).
    /// </summary>
    protected override void EnrichActivity(Activity activity, StopRideCommand command)
        => activity.SetTag(RideTelemetryAttributes.State, _store.CurrentState.ToString());
}
