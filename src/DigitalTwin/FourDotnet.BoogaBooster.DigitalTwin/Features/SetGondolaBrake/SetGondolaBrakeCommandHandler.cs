using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetGondolaBrake;

/// <summary>Works a gondola's brake through the ride store.</summary>
public sealed class SetGondolaBrakeCommandHandler : CommandHandler<SetGondolaBrakeCommand>
{
    private readonly IRideStore _store;

    public SetGondolaBrakeCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(SetGondolaBrakeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetGondolaBrake(command.HubIndex, command.GondolaIndex, command.Brake);
        return Task.CompletedTask;
    }

    /// <summary>Records which of the sixteen gondolas was addressed, and to what.</summary>
    protected override void EnrichActivity(Activity activity, SetGondolaBrakeCommand command)
    {
        activity.SetTag(RideTelemetryAttributes.HubIndex, command.HubIndex);
        activity.SetTag(RideTelemetryAttributes.GondolaIndex, command.GondolaIndex);
        activity.SetTag(RideTelemetryAttributes.GondolaBrake, command.Brake.ToString());
    }
}
