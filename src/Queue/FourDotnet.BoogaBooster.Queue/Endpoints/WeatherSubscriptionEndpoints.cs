using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using FourDotnet.BoogaBooster.Queue.Filling;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace FourDotnet.BoogaBooster.Queue.Endpoints;

/// <summary>
/// The Queue module's subscription to the weather-update integration event
/// (ADR-0007). Mapped as part of <see cref="QueueEndpoints.MapQueueEndpoints"/> and
/// discovered by the Dapr sidecar via <c>WithTopic</c> + the host's
/// <c>MapSubscribeHandler()</c>; no static subscription YAML is used.
/// </summary>
internal static class WeatherSubscriptionEndpoints
{
    internal static IEndpointRouteBuilder MapWeatherSubscription(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapPost("/integration-events/weather-updated", HandleWeatherUpdate)
            .WithTopic(
                IntegrationMessagingDefaults.PubSubName,
                TopicNameResolver.Resolve<WeatherUpdateIntegrationEvent>())
            .WithTags("Queue")
            .ExcludeFromDescription();

        return endpoints;
    }

    /// <summary>
    /// Records the latest weather into the shared <see cref="IWeatherInfluence"/>
    /// state and acknowledges the message. Kept free of queue-domain logic — the
    /// filler applies the value on its own schedule.
    /// </summary>
    internal static Ok HandleWeatherUpdate(
        WeatherUpdateIntegrationEvent @event,
        IWeatherInfluence weatherInfluence)
    {
        ArgumentNullException.ThrowIfNull(@event);

        weatherInfluence.Update(@event.NiceWeather);
        return TypedResults.Ok();
    }
}
