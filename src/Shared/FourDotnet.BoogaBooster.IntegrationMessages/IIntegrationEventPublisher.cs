namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// Publishes integration events to the message broker via Dapr pub/sub. The
/// topic each event is published to is resolved from its
/// <see cref="TopicNameAttribute"/>.
/// </summary>
public interface IIntegrationEventPublisher
{
    /// <summary>
    /// Publishes <paramref name="event"/> to the topic declared on its
    /// <see cref="TopicNameAttribute"/>.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}
