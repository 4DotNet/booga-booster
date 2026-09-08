using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEnginePower;

/// <summary>Applies the commanded hub-engine power to all four hubs.</summary>
public sealed class SetHubEnginePowerCommandHandler : CommandHandler<SetHubEnginePowerCommand>
{
    private readonly IRideStore _store;

    public SetHubEnginePowerCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(SetHubEnginePowerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetHubEnginePower(new EnginePower(command.Percent));
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records the throttle asked for and that it was the hub drives, so this span
    /// is distinguishable from the main-engine one at a glance. All four hubs take
    /// the same setting, so no hub index belongs here.
    /// </summary>
    protected override void EnrichActivity(Activity activity, SetHubEnginePowerCommand command)
    {
        activity.SetTag(RideTelemetryAttributes.Engine, RideTelemetryAttributes.HubEngines);
        activity.SetTag(RideTelemetryAttributes.EnginePowerPercent, command.Percent);
    }
}
