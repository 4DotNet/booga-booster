using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEnginePower;

/// <summary>Applies the commanded hub-engine power to all four hubs.</summary>
public sealed class SetHubEnginePowerCommandHandler : CommandHandler<SetHubEnginePowerCommand>
{
    private readonly IRideStore _store;

    public SetHubEnginePowerCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public override Task HandleAsync(SetHubEnginePowerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetHubEnginePower(new EnginePower(command.Percent));
        return Task.CompletedTask;
    }
}
