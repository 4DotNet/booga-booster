namespace FourDotnet.BoogaBooster.Core.Cqrs;

/// <summary>
/// Handles exactly one query type and always returns a response (ADR-0005).
/// Resolved via dependency injection by the dispatching endpoint.
/// </summary>
/// <typeparam name="TQuery">The query this handler handles.</typeparam>
/// <typeparam name="TResponse">The response the query returns.</typeparam>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : Query<TResponse>
{
    /// <summary>Executes <paramref name="query"/> and returns its response.</summary>
    Task<TResponse> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
