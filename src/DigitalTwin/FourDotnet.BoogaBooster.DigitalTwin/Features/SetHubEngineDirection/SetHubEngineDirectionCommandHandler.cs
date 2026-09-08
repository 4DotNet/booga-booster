using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEngineDirection;

/// <summary>Applies the commanded hub-engine rotation direction to all four hubs.</summary>
public sealed class SetHubEngineDirectionCommandHandler : CommandHandler<SetHubEngineDirectionCommand>
{
    private readonly IRideStore _store;

    public SetHubEngineDirectionCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(SetHubEngineDirectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        _store.SetHubEngineDirection(command.Direction);
        return Task.CompletedTask;
    }
}
