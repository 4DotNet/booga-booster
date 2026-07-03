using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// The single home for the simulation's tuning constants: the moderate defaults,
/// each regime's target readings and duration, the per-tick reversion fraction,
/// the random-noise amplitudes, and the tick interval. Keeping them together
/// makes the simulation safe to tune.
/// </summary>
public static class WeatherDefaults
{
    // --- Moderate defaults (the Calm target) ---
    public const double DefaultTemperatureCelsius = 20d;
    public const int DefaultWindBeaufort = 2;
    public const int DefaultSunshinePercent = 70;

    // --- Simulation cadence ---
    /// <summary>Wall-clock time represented by one simulation tick.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(5);

    // --- Event durations ---
    /// <summary>A precipitation event lasts about 15 minutes.</summary>
    public static readonly TimeSpan PrecipitationDuration = TimeSpan.FromMinutes(15);

    /// <summary>A strong-wind event lasts up to about 10 minutes.</summary>
    public static readonly TimeSpan StrongWindDuration = TimeSpan.FromMinutes(10);

    // --- Drift tuning ---
    /// <summary>Fraction of the gap to the target closed each tick (mean-reversion).</summary>
    public const double ReversionFraction = 0.25d;

    public const double TemperatureNoise = 0.5d;
    public const double WindNoise = 0.4d;
    public const double SunshineNoise = 2.5d;
    public const double PrecipitationNoise = 3d;

    /// <summary>At or below this intensity a tapering precipitation is considered stopped.</summary>
    public const int PrecipitationStoppedThreshold = 2;

    /// <summary>Target intensity while a precipitation event is active.</summary>
    public const int PrecipitationActiveIntensity = 70;

    /// <summary>Intensity the moment a precipitation event is triggered (it then ramps up).</summary>
    public const int PrecipitationOnsetIntensity = 20;

    /// <summary>The readings a regime pulls the weather toward.</summary>
    /// <param name="TemperatureCelsius">Target temperature.</param>
    /// <param name="WindBeaufort">Target wind.</param>
    /// <param name="SunshinePercent">Target sunshine.</param>
    /// <param name="PrecipitationIntensity">Target precipitation intensity.</param>
    public sealed record RegimeProfile(
        double TemperatureCelsius,
        int WindBeaufort,
        int SunshinePercent,
        int PrecipitationIntensity);

    private static readonly RegimeProfile Calm = new(
        DefaultTemperatureCelsius, DefaultWindBeaufort, DefaultSunshinePercent, 0);

    // Precipitation: cooler, wind picks up a bit, sunshine dims, precipitation falls.
    private static readonly RegimeProfile Precipitation = new(
        TemperatureCelsius: 12d, WindBeaufort: 4, SunshinePercent: 40,
        PrecipitationIntensity: PrecipitationActiveIntensity);

    // Strong wind: very strong wind, sunshine fades, temperature drops, no precipitation.
    private static readonly RegimeProfile StrongWind = new(
        TemperatureCelsius: 14d, WindBeaufort: 9, SunshinePercent: 25,
        PrecipitationIntensity: 0);

    /// <summary>Returns the target readings for the given regime.</summary>
    public static RegimeProfile ProfileFor(WeatherRegime regime) => regime switch
    {
        WeatherRegime.Precipitation => Precipitation,
        WeatherRegime.StrongWind => StrongWind,
        _ => Calm,
    };
}
