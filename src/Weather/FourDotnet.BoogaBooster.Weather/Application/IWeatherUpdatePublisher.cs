using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Application;

/// <summary>
/// Publishes a weather update to the rest of the system. This is the module's
/// seam over the central integration-messages publisher, so the simulation loop
/// and event handlers depend on an intent-revealing abstraction rather than on
/// the transport.
/// </summary>
public interface IWeatherUpdatePublisher
{
    /// <summary>Publishes the given conditions as a weather update.</summary>
    Task PublishAsync(WeatherConditionDto snapshot, CancellationToken cancellationToken);
}
