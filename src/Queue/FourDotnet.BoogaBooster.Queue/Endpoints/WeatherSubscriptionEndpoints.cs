using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using FourDotnet.BoogaBooster.Queue.Features.RecordWeatherUpdate;
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
    /// Maps the integration event to a <see cref="RecordWeatherUpdateCommand"/>,
    /// dispatches it to the injected handler and acknowledges the message. Like
    /// every endpoint in the module it holds no logic of its own (ADR-0005).
    /// </summary>
    internal static async Task<Ok> HandleWeatherUpdate(
        WeatherUpdateIntegrationEvent @event,
        ICommandHandler<RecordWeatherUpdateCommand> handler,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(@event);
        ArgumentNullException.ThrowIfNull(handler);

        await handler.HandleAsync(new RecordWeatherUpdateCommand(@event.NiceWeather), cancellationToken);
        return TypedResults.Ok();
    }
}
