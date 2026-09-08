using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetMainEngineDirection;

/// <summary>Applies the commanded main-engine rotation direction to the ride.</summary>
public sealed class SetMainEngineDirectionCommandHandler : CommandHandler<SetMainEngineDirectionCommand>
{
    private readonly IRideStore _store;

    public SetMainEngineDirectionCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(SetMainEngineDirectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetMainEngineDirection(command.Direction);
        return Task.CompletedTask;
    }

    /// <summary>Records the direction asked for and that it was the mill's drive.</summary>
    protected override void EnrichActivity(Activity activity, SetMainEngineDirectionCommand command)
    {
        activity.SetTag(RideTelemetryAttributes.Engine, RideTelemetryAttributes.MainEngine);
        activity.SetTag(RideTelemetryAttributes.EngineDirection, command.Direction.ToString());
    }
}
