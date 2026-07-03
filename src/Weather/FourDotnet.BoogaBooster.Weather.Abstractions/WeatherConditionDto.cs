namespace FourDotnet.BoogaBooster.Weather.Abstractions;

/// <summary>
/// A snapshot of the current weather conditions, shared across modules.
/// </summary>
/// <param name="TemperatureCelsius">Air temperature in degrees Celsius.</param>
/// <param name="WindBeaufort">Wind speed on the Beaufort scale (0–12).</param>
/// <param name="SunshinePercent">Sunshine intensity as a percentage (0–100).</param>
/// <param name="Precipitation">The kind of precipitation currently falling.</param>
/// <param name="Regime">The active weather regime.</param>
/// <param name="NiceWeather">
/// A "nice weather" indicator in the range [0, 1]: 1 for pleasant, moderate
/// weather and 0 for bad weather such as a severe storm or heavy precipitation.
/// </param>
public sealed record WeatherConditionDto(
    double TemperatureCelsius,
    int WindBeaufort,
    int SunshinePercent,
    PrecipitationType Precipitation,
    WeatherRegime Regime,
    float NiceWeather);
