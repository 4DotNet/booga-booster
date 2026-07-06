using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.BrakeEngines;

/// <summary>Applies the engine brakes through the store (cuts mill and hub power).</summary>
public sealed class BrakeEnginesCommandHandler : CommandHandler<BrakeEnginesCommand>
{
    private readonly IRideStore _store;

    public BrakeEnginesCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public override Task HandleAsync(BrakeEnginesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.BrakeEngines();
        return Task.CompletedTask;
    }
}
