using Dapr.Client;

namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// Default <see cref="IIntegrationEventPublisher"/> that publishes through Dapr
/// pub/sub. The topic is resolved from the event's <see cref="TopicNameAttribute"/>
/// and messages are sent to the component named
/// <see cref="IntegrationMessagingDefaults.PubSubName"/>.
/// </summary>
internal sealed class DaprIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly DaprClient _daprClient;

    public DaprIntegrationEventPublisher(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Resolve from the runtime type so events published through a base
        // reference still land on the correct topic.
        var topic = TopicNameResolver.Resolve(@event.GetType());

        return _daprClient.PublishEventAsync(
            IntegrationMessagingDefaults.PubSubName,
            topic,
            @event,
            cancellationToken);
    }
}
