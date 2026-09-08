namespace FourDotnet.BoogaBooster.Weather.Observability;

/// <summary>
/// The span attribute names the Weather module records, and the bounded values its
/// disturbance-kind metric tag takes. Declared as constants in one place so no call
/// site repeats a literal, a rename is a single edit, and the tests assert against
/// the same name the production code writes.
/// </summary>
internal static class WeatherTelemetryAttributes
{
    /// <summary>The active weather regime.</summary>
    internal const string Regime = "weather.regime";

    /// <summary>The nice-weather reading in [0, 1].</summary>
    internal const string NiceWeather = "weather.nice_weather";

    /// <summary>The kind of precipitation a command requested or the store reported.</summary>
    internal const string PrecipitationType = "weather.precipitation.type";

    /// <summary>
    /// Wind speed on the Beaufort scale — the unit the <c>Wind</c> value object holds,
    /// so the span reports the same number the domain does.
    /// </summary>
    internal const string WindBeaufort = "weather.wind.beaufort";

    /// <summary>Which disturbance was raised. Doubles as the metric tag key.</summary>
    internal const string DisturbanceKind = "weather.disturbance.kind";

    /// <summary>The <see cref="DisturbanceKind"/> value for a precipitation event.</summary>
    internal const string PrecipitationDisturbance = "precipitation";

    /// <summary>The <see cref="DisturbanceKind"/> value for a strong-wind event.</summary>
    internal const string StrongWindDisturbance = "strong-wind";
}
