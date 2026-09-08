using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEngineDirection;

/// <summary>Applies the commanded hub-engine rotation direction to all four hubs.</summary>
public sealed class SetHubEngineDirectionCommandHandler : CommandHandler<SetHubEngineDirectionCommand>
{
    private readonly IRideStore _store;

    public SetHubEngineDirectionCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(SetHubEngineDirectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetHubEngineDirection(command.Direction);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records the direction asked for and that it was the hub drives. All four hubs
    /// take the same setting, so no hub index belongs here.
    /// </summary>
    protected override void EnrichActivity(Activity activity, SetHubEngineDirectionCommand command)
    {
        activity.SetTag(RideTelemetryAttributes.Engine, RideTelemetryAttributes.HubEngines);
        activity.SetTag(RideTelemetryAttributes.EngineDirection, command.Direction.ToString());
    }
}
