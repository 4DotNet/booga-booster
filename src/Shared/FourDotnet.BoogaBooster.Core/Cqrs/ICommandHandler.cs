namespace FourDotnet.BoogaBooster.Core.Cqrs;

/// <summary>
/// Handles exactly one command type (ADR-0005). Resolved via dependency
/// injection by the dispatching endpoint.
/// </summary>
/// <typeparam name="TCommand">The command this handler handles.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : Command
{
    /// <summary>Executes <paramref name="command"/>.</summary>
    Task HandleAsync(TCommand command, CancellationToken cancellationToken);
}
