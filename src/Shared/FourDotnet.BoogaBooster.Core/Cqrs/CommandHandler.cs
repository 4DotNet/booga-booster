using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Observability;

namespace FourDotnet.BoogaBooster.Core.Cqrs;

/// <summary>
/// Base class for command handlers (ADR-0005). Handlers derive from this shared
/// base rather than implementing the interface directly, so the pattern is
/// uniform across modules.
/// </summary>
/// <remarks>
/// The base owns the observability plumbing mandated by ADR-0009 — it starts the
/// activity, times the invocation and records the outcome counter and duration
/// histogram — so a concrete handler implements <see cref="ExecuteAsync"/> and adds
/// only its own span tags (<see cref="EnrichActivity"/>) and domain metrics.
/// </remarks>
/// <typeparam name="TCommand">The command this handler handles.</typeparam>
public abstract class CommandHandler<TCommand> : ICommandHandler<TCommand>
    where TCommand : Command
{
    private const string OperationKind = "command";

    private readonly string _operationName;

    /// <summary>Derives the handler's operation name from its type name.</summary>
    protected CommandHandler() => _operationName = HandlerOperationName.For(GetType());

    /// <summary>
    /// The name the activity and metrics report this handler under. Defaults to the
    /// type name without its <c>CommandHandler</c> suffix.
    /// </summary>
    protected virtual string OperationName => _operationName;

    /// <inheritdoc />
    public async Task HandleAsync(TCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var scope = HandlerTelemetryScope.Start(OperationName, OperationKind);

        try
        {
            if (scope.Activity is { } activity)
            {
                EnrichActivity(activity, command);
            }

            await ExecuteAsync(command, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            scope.Failed(exception);
            throw;
        }
        finally
        {
            scope.Complete();
        }
    }

    /// <summary>Executes <paramref name="command"/>. The base class instruments the call.</summary>
    protected abstract Task ExecuteAsync(TCommand command, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the handler's own attributes to the span — the identifiers and inputs
    /// worth having during an incident. Called only when something is listening, and
    /// never with secrets or personal data.
    /// </summary>
    protected virtual void EnrichActivity(Activity activity, TCommand command)
    {
    }
}
