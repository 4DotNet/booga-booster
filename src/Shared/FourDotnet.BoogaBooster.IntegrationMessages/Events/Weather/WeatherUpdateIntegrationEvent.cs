namespace FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;

/// <summary>
/// Published by the Weather module on every weather update that changes the
/// conditions, so other services can react to the new weather.
/// </summary>
/// <param name="TemperatureCelsius">Air temperature in degrees Celsius.</param>
/// <param name="WindBeaufort">Wind speed on the Beaufort scale (0–12).</param>
/// <param name="SunshinePercent">Sunshine intensity as a percentage (0–100).</param>
/// <param name="Precipitation">The kind of precipitation currently falling (e.g. None, Rain, Snow, Hail).</param>
/// <param name="Regime">The active weather regime (e.g. Calm, Precipitation, StrongWind).</param>
/// <param name="NiceWeather">
/// A "nice weather" indicator in the range [0, 1]: 1 for pleasant, moderate
/// weather and 0 for bad weather such as a severe storm or heavy precipitation.
/// </param>
[TopicName("weather-updated")]
public sealed record WeatherUpdateIntegrationEvent(
    double TemperatureCelsius,
    int WindBeaufort,
    int SunshinePercent,
    string Precipitation,
    string Regime,
    float NiceWeather) : IIntegrationEvent;
