using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.BrakeEngines;

/// <summary>Sets the engine brake through the store (engaging cuts mill and hub power).</summary>
public sealed class BrakeEnginesCommandHandler : CommandHandler<BrakeEnginesCommand>
{
    private readonly IRideStore _store;

    public BrakeEnginesCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(BrakeEnginesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetEngineBrakes(command.Engaged);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records the brake state asked for alongside the one already in effect, so a
    /// command that changed nothing is visible as the no-op it was — the difference
    /// between "the brake never engaged" and "the brake was engaged all along".
    /// </summary>
    protected override void EnrichActivity(Activity activity, BrakeEnginesCommand command)
    {
        activity.SetTag(RideTelemetryAttributes.BrakeEngaged, command.Engaged);
        activity.SetTag(RideTelemetryAttributes.BrakeEngagedBefore, _store.GetTelemetry().BrakesEngaged);
    }
}
