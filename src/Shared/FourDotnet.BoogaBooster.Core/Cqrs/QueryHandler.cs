namespace FourDotnet.BoogaBooster.Core.Cqrs;

/// <summary>
/// Base class for query handlers (ADR-0005). Handlers derive from this shared
/// base rather than implementing the interface directly, so the pattern is
/// uniform across modules.
/// </summary>
/// <typeparam name="TQuery">The query this handler handles.</typeparam>
/// <typeparam name="TResponse">The response the query returns.</typeparam>
public abstract class QueryHandler<TQuery, TResponse> : IQueryHandler<TQuery, TResponse>
    where TQuery : Query<TResponse>
{
    /// <inheritdoc />
    public abstract Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
