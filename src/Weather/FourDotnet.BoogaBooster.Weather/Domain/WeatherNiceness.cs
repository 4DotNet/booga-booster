using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// Computes a "nice weather" indicator in the range [0, 1]: <c>1</c> for
/// pleasant, moderate weather and <c>0</c> for bad weather such as a severe
/// storm or heavy precipitation. The score multiplies independent penalties, so
/// any single severe factor (very high wind, intense precipitation, extreme
/// temperature) drives the result toward zero.
/// </summary>
public static class WeatherNiceness
{
    /// <summary>Beaufort at or below which wind does not detract from niceness.</summary>
    private const double PleasantWindBeaufort = 3d;

    /// <summary>Beaufort at or above which wind (a storm) makes weather fully unpleasant.</summary>
    private const double StormWindBeaufort = 9d;

    /// <summary>Degrees away from the moderate default at which temperature is fully unpleasant.</summary>
    private const double TemperatureTolerance = 25d;

    /// <summary>
    /// Calculates the niceness indicator from the current readings.
    /// </summary>
    public static float Calculate(
        double temperatureCelsius,
        int windBeaufort,
        PrecipitationType precipitationType,
        int precipitationIntensity)
    {
        var temperatureScore = 1d - Clamp(
            Math.Abs(temperatureCelsius - WeatherDefaults.DefaultTemperatureCelsius) / TemperatureTolerance, 0d, 1d);

        var windScore = 1d - Clamp(
            (windBeaufort - PleasantWindBeaufort) / (StormWindBeaufort - PleasantWindBeaufort), 0d, 1d);

        var precipitationScore = precipitationType == PrecipitationType.None
            ? 1d
            : 1d - Clamp(precipitationIntensity / 100d, 0d, 1d);

        var niceness = temperatureScore * windScore * precipitationScore;
        return (float)Math.Round(Clamp(niceness, 0d, 1d), 3);
    }

    private static double Clamp(double value, double min, double max)
        => Math.Min(Math.Max(value, min), max);
}
