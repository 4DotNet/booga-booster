namespace FourDotnet.BoogaBooster.Weather.Abstractions;

/// <summary>
/// Exposes the current weather conditions to other modules without them
/// referencing the Weather module project (ADR-0004). The returned snapshot is
/// consistent with what the Weather read endpoint serves.
/// </summary>
public interface IWeatherConditionProvider
{
    /// <summary>Returns the current weather conditions.</summary>
    WeatherConditionDto GetCurrentConditions();
}
