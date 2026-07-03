namespace FourDotnet.BoogaBooster.Core.Cqrs;

/// <summary>
/// Base class for command handlers (ADR-0005). Handlers derive from this shared
/// base rather than implementing the interface directly, so the pattern is
/// uniform across modules.
/// </summary>
/// <typeparam name="TCommand">The command this handler handles.</typeparam>
public abstract class CommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : Command
{
    /// <inheritdoc />
    public abstract Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}
