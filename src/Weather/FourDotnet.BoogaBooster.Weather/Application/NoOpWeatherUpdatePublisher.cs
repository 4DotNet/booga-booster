using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Application;

/// <summary>
/// A <see cref="IWeatherUpdatePublisher"/> that does nothing. Lets the module run
/// standalone (without the integration-messages transport wired up) for local
/// development and tests.
/// </summary>
public sealed class NoOpWeatherUpdatePublisher : IWeatherUpdatePublisher
{
    public Task PublishAsync(WeatherConditionDto snapshot, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
