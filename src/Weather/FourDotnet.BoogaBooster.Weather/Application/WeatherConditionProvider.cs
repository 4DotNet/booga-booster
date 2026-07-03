using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Application;

/// <summary>
/// Exposes the current conditions to other modules (ADR-0004) by reading the
/// consistent snapshot from the <see cref="IWeatherStore"/>.
/// </summary>
public sealed class WeatherConditionProvider : IWeatherConditionProvider
{
    private readonly IWeatherStore _store;

    public WeatherConditionProvider(IWeatherStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public WeatherConditionDto GetCurrentConditions() => _store.GetSnapshot();
}
