namespace FourDotnet.BoogaBooster.Core.Cqrs;

/// <summary>
/// Base type for a query — an intent to read state (ADR-0005). A query always
/// has a response, captured by <typeparamref name="TResponse"/>. Queries are
/// plain data; the behavior lives in the matching
/// <see cref="IQueryHandler{TQuery, TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">The type returned when the query is handled.</typeparam>
public abstract record Query<TResponse>;
