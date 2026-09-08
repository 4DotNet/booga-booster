using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Observability;

namespace FourDotnet.BoogaBooster.Core.Cqrs;

/// <summary>
/// Base class for query handlers (ADR-0005). Handlers derive from this shared
/// base rather than implementing the interface directly, so the pattern is
/// uniform across modules.
/// </summary>
/// <remarks>
/// The base owns the observability plumbing mandated by ADR-0009 — it starts the
/// activity, times the invocation and records the outcome counter and duration
/// histogram — so a concrete handler implements <see cref="ExecuteAsync"/> and adds
/// only its own span tags (<see cref="EnrichActivity"/> for the request,
/// <see cref="EnrichActivityWithResponse"/> for what was read) and domain metrics.
/// </remarks>
/// <typeparam name="TQuery">The query this handler handles.</typeparam>
/// <typeparam name="TResponse">The response the query returns.</typeparam>
public abstract class QueryHandler<TQuery, TResponse> : IQueryHandler<TQuery, TResponse>
    where TQuery : Query<TResponse>
{
    private const string OperationKind = "query";

    private readonly string _operationName;

    /// <summary>Derives the handler's operation name from its type name.</summary>
    protected QueryHandler() => _operationName = HandlerOperationName.For(GetType());

    /// <summary>
    /// The name the activity and metrics report this handler under. Defaults to the
    /// type name without its <c>QueryHandler</c> suffix.
    /// </summary>
    protected virtual string OperationName => _operationName;

    /// <inheritdoc />
    public async Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var scope = HandlerTelemetryScope.Start(OperationName, OperationKind);

        try
        {
            if (scope.Activity is { } activity)
            {
                EnrichActivity(activity, query);
            }

            var response = await ExecuteAsync(query, cancellationToken).ConfigureAwait(false);

            if (scope.Activity is { } completed)
            {
                EnrichActivityWithResponse(completed, response);
            }

            return response;
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

    /// <summary>
    /// Executes <paramref name="query"/> and returns its response. The base class
    /// instruments the call.
    /// </summary>
    protected abstract Task<TResponse> ExecuteAsync(TQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// Adds the handler's own attributes to the span — the identifiers and inputs
    /// worth having during an incident. Called only when something is listening, and
    /// never with secrets or personal data.
    /// </summary>
    protected virtual void EnrichActivity(Activity activity, TQuery query)
    {
    }

    /// <summary>
    /// Adds attributes describing what the query read — counts and sizes, not the
    /// payload itself. Called after a successful read, only when something is listening.
    /// </summary>
    protected virtual void EnrichActivityWithResponse(Activity activity, TResponse response)
    {
    }
}
