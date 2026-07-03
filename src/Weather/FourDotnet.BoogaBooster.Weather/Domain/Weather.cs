using FourDotnet.BoogaBooster.Core.Domain;
using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// The single world-weather aggregate (ADR-0003). Owns the current readings as
/// validated value objects, the active regime, and the remaining time on any
/// active event. Its readings only change through the validated <c>SetX()</c>
/// operations, and the whole simulation advances through <see cref="Advance"/>.
/// </summary>
public sealed class Weather : DomainModel
{
    private PrecipitationType _activePrecipitationType = PrecipitationType.None;

    private Weather(
        Temperature temperature,
        Wind wind,
        Sunshine sunshine,
        Precipitation precipitation)
        : base(isNew: true)
    {
        Temperature = temperature;
        Wind = wind;
        Sunshine = sunshine;
        Precipitation = precipitation;
        Regime = WeatherRegime.Calm;
        RemainingEventDuration = TimeSpan.Zero;
    }

    /// <summary>Current air temperature.</summary>
    public Temperature Temperature { get; private set; }

    /// <summary>Current wind speed.</summary>
    public Wind Wind { get; private set; }

    /// <summary>Current sunshine intensity.</summary>
    public Sunshine Sunshine { get; private set; }

    /// <summary>Current precipitation.</summary>
    public Precipitation Precipitation { get; private set; }

    /// <summary>The regime the simulation is currently drifting toward.</summary>
    public WeatherRegime Regime { get; private set; }

    /// <summary>Time left on the active event; <see cref="TimeSpan.Zero"/> when calm.</summary>
    public TimeSpan RemainingEventDuration { get; private set; }

    /// <summary>Creates the world weather at the moderate defaults.</summary>
    public static Weather CreateDefault() => new(
        new Temperature(WeatherDefaults.DefaultTemperatureCelsius),
        new Wind(WeatherDefaults.DefaultWindBeaufort),
        new Sunshine(WeatherDefaults.DefaultSunshinePercent),
        Precipitation.None);

    /// <summary>Sets the temperature (validated by the value object).</summary>
    public bool SetTemperature(Temperature value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ApplyChange(Temperature, value, v => Temperature = v);
    }

    /// <summary>Sets the wind (validated by the value object).</summary>
    public bool SetWind(Wind value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ApplyChange(Wind, value, v => Wind = v);
    }

    /// <summary>Sets the sunshine (validated by the value object).</summary>
    public bool SetSunshine(Sunshine value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ApplyChange(Sunshine, value, v => Sunshine = v);
    }

    /// <summary>Sets the precipitation (validated by the value object).</summary>
    public bool SetPrecipitation(Precipitation value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return ApplyChange(Precipitation, value, v => Precipitation = v);
    }

    /// <summary>
    /// Begins a precipitation event of the given type. The regime becomes
    /// <see cref="WeatherRegime.Precipitation"/> for about 15 minutes, after
    /// which the weather reverts to the defaults.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// Thrown when <paramref name="type"/> is <see cref="PrecipitationType.None"/>.
    /// </exception>
    public void StartPrecipitation(PrecipitationType type)
    {
        if (type == PrecipitationType.None)
        {
            throw new DomainValidationException(
                "A precipitation event requires a precipitation type of Rain, Snow, or Hail.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainValidationException($"Unknown precipitation type '{type}'.");
        }

        _activePrecipitationType = type;
        Regime = WeatherRegime.Precipitation;
        RemainingEventDuration = WeatherDefaults.PrecipitationDuration;

        // Reflect the chosen precipitation immediately so a reading taken right
        // after triggering already shows it falling (it then ramps up per tick).
        SetPrecipitation(new Precipitation(type, WeatherDefaults.PrecipitationOnsetIntensity));
    }

    /// <summary>
    /// Begins a strong-wind event. The regime becomes
    /// <see cref="WeatherRegime.StrongWind"/> for up to about 10 minutes, after
    /// which the weather reverts to the defaults.
    /// </summary>
    public void StartStrongWind()
    {
        Regime = WeatherRegime.StrongWind;
        RemainingEventDuration = WeatherDefaults.StrongWindDuration;
    }

    /// <summary>
    /// Advances the simulation by <paramref name="elapsed"/>: expires any active
    /// event, then nudges every reading a fraction of the way toward the active
    /// regime's target plus a small bounded random sample.
    /// </summary>
    /// <returns><c>true</c> when the conditions actually changed this tick.</returns>
    public bool Advance(TimeSpan elapsed, IWeatherSampler sampler)
    {
        ArgumentNullException.ThrowIfNull(sampler);

        var regimeChanged = ExpireActiveEvent(elapsed);
        var profile = WeatherDefaults.ProfileFor(Regime);

        var temperatureChanged = SetTemperature(NextTemperature(profile, sampler));
        var windChanged = SetWind(NextWind(profile, sampler));
        var sunshineChanged = SetSunshine(NextSunshine(profile, sampler));
        var precipitationChanged = SetPrecipitation(NextPrecipitation(profile, sampler));

        return regimeChanged
            || temperatureChanged
            || windChanged
            || sunshineChanged
            || precipitationChanged;
    }

    /// <summary>Projects the aggregate onto the cross-module snapshot DTO.</summary>
    public WeatherConditionDto ToConditionDto() => new(
        Temperature.Celsius,
        Wind.Beaufort,
        Sunshine.Percent,
        Precipitation.Type,
        Regime,
        WeatherNiceness.Calculate(Temperature.Celsius, Wind.Beaufort, Precipitation.Type, Precipitation.Intensity));

    private bool ExpireActiveEvent(TimeSpan elapsed)
    {
        if (Regime == WeatherRegime.Calm)
        {
            return false;
        }

        RemainingEventDuration -= elapsed;
        if (RemainingEventDuration > TimeSpan.Zero)
        {
            return false;
        }

        Regime = WeatherRegime.Calm;
        RemainingEventDuration = TimeSpan.Zero;
        return true;
    }

    private Temperature NextTemperature(WeatherDefaults.RegimeProfile profile, IWeatherSampler sampler)
    {
        var next = Drift(Temperature.Celsius, profile.TemperatureCelsius, WeatherDefaults.TemperatureNoise, sampler);
        return new Temperature(Clamp(next, Temperature.MinCelsius, Temperature.MaxCelsius));
    }

    private Wind NextWind(WeatherDefaults.RegimeProfile profile, IWeatherSampler sampler)
    {
        var next = Drift(Wind.Beaufort, profile.WindBeaufort, WeatherDefaults.WindNoise, sampler);
        return new Wind((int)Clamp(Round(next), Wind.MinBeaufort, Wind.MaxBeaufort));
    }

    private Sunshine NextSunshine(WeatherDefaults.RegimeProfile profile, IWeatherSampler sampler)
    {
        var next = Drift(Sunshine.Percent, profile.SunshinePercent, WeatherDefaults.SunshineNoise, sampler);
        return new Sunshine((int)Clamp(Round(next), Sunshine.MinPercent, Sunshine.MaxPercent));
    }

    private Precipitation NextPrecipitation(WeatherDefaults.RegimeProfile profile, IWeatherSampler sampler)
    {
        var next = Drift(Precipitation.Intensity, profile.PrecipitationIntensity, WeatherDefaults.PrecipitationNoise, sampler);
        var intensity = (int)Clamp(Round(next), Precipitation.MinIntensity, Precipitation.MaxIntensity);

        if (Regime == WeatherRegime.Precipitation)
        {
            // While the event is active precipitation keeps falling, so never let it round to nothing.
            return new Precipitation(_activePrecipitationType, Math.Max(intensity, 1));
        }

        // Not raining/snowing/hailing: keep the current type while it tapers off, then stop.
        if (Precipitation.Type == PrecipitationType.None || intensity <= WeatherDefaults.PrecipitationStoppedThreshold)
        {
            return Precipitation.None;
        }

        return new Precipitation(Precipitation.Type, intensity);
    }

    private static double Drift(double current, double target, double noiseAmplitude, IWeatherSampler sampler)
        => current + ((target - current) * WeatherDefaults.ReversionFraction) + (sampler.Sample() * noiseAmplitude);

    private static double Round(double value) => Math.Round(value, MidpointRounding.AwayFromZero);

    private static double Clamp(double value, double min, double max)
        => Math.Min(Math.Max(value, min), max);
}
