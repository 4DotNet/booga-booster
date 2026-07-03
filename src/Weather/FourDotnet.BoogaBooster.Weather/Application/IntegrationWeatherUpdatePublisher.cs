using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Application;

/// <summary>
/// Default <see cref="IWeatherUpdatePublisher"/> that maps the current snapshot
/// to a <see cref="WeatherUpdateIntegrationEvent"/> and publishes it through the
/// central integration-messages publisher (Dapr pub/sub over RabbitMQ).
/// </summary>
public sealed class IntegrationWeatherUpdatePublisher : IWeatherUpdatePublisher
{
    private readonly IIntegrationEventPublisher _publisher;

    public IntegrationWeatherUpdatePublisher(IIntegrationEventPublisher publisher)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    public Task PublishAsync(WeatherConditionDto snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var integrationEvent = new WeatherUpdateIntegrationEvent(
            snapshot.TemperatureCelsius,
            snapshot.WindBeaufort,
            snapshot.SunshinePercent,
            snapshot.Precipitation.ToString(),
            snapshot.Regime.ToString(),
            snapshot.NiceWeather);

        return _publisher.PublishAsync(integrationEvent, cancellationToken);
    }
}
