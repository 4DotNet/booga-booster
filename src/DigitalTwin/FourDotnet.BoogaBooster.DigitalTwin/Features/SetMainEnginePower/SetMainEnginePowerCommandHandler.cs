using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetMainEnginePower;

/// <summary>Applies the commanded main-engine power to the ride.</summary>
public sealed class SetMainEnginePowerCommandHandler : CommandHandler<SetMainEnginePowerCommand>
{
    private readonly IRideStore _store;

    public SetMainEnginePowerCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(SetMainEnginePowerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetMainEnginePower(new EnginePower(command.Percent));
        return Task.CompletedTask;
    }
}
