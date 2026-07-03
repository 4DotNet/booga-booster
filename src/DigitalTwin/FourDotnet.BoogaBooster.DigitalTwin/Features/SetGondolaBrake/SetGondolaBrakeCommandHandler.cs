using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetGondolaBrake;

/// <summary>Works a gondola's brake through the ride store.</summary>
public sealed class SetGondolaBrakeCommandHandler : CommandHandler<SetGondolaBrakeCommand>
{
    private readonly IRideStore _store;

    public SetGondolaBrakeCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public override Task HandleAsync(SetGondolaBrakeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetGondolaBrake(command.HubIndex, command.GondolaIndex, command.Brake);
        return Task.CompletedTask;
    }
}
