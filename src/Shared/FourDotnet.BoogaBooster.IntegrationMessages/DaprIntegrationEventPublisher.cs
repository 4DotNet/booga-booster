using System.Diagnostics;
using System.Diagnostics.Metrics;
using Dapr.Client;
using FourDotnet.BoogaBooster.Core.Observability;

namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// Default <see cref="IIntegrationEventPublisher"/> that publishes through Dapr
/// pub/sub. The topic is resolved from the event's <see cref="TopicNameAttribute"/>
/// and messages are sent to the component named
/// <see cref="IntegrationMessagingDefaults.PubSubName"/>.
/// </summary>
/// <remarks>
/// Each publish is a producer span from the shared activity source (ADR-0009), so it
/// nests under the handler activity that caused it via <see cref="Activity.Current"/>
/// and an event that never left the process is visible in the trace.
/// </remarks>
internal sealed class DaprIntegrationEventPublisher : IIntegrationEventPublisher
{
    private const string PublishOperationName = "PublishIntegrationEvent";

    private readonly DaprClient _daprClient;

    public DaprIntegrationEventPublisher(DaprClient daprClient)
    {
        _daprClient = daprClient;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        // Resolve from the runtime type so events published through a base
        // reference still land on the correct topic.
        var eventType = @event.GetType();
        var topic = TopicNameResolver.Resolve(eventType);

        using var activity = BoogaBoosterTelemetry.ActivitySource.StartActivity(
            PublishOperationName,
            ActivityKind.Producer);

        if (activity is not null)
        {
            activity.SetTag(MessagingTelemetryAttributes.System, MessagingTelemetryAttributes.DaprSystem);
            activity.SetTag(MessagingTelemetryAttributes.DestinationName, topic);
            activity.SetTag(MessagingTelemetryAttributes.ComponentName, IntegrationMessagingDefaults.PubSubName);
            activity.SetTag(MessagingTelemetryAttributes.EventType, eventType.Name);
        }

        var outcome = TelemetryOutcome.Ok;

        try
        {
            await _daprClient.PublishEventAsync(
                    IntegrationMessagingDefaults.PubSubName,
                    topic,
                    @event,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            outcome = TelemetryOutcome.Error;
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
        finally
        {
            // Topic and outcome only: an event id would multiply the series per message.
            BoogaBoosterTelemetry.IntegrationEventsPublished.Add(
                1,
                new TagList { { "topic", topic }, { "outcome", outcome } });
        }
    }
}
