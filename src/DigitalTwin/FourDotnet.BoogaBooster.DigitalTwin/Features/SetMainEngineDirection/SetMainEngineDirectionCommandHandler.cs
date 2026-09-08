using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

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
}
