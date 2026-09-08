using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

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
}
