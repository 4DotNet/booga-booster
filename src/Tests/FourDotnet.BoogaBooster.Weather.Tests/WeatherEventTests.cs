using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Domain;
using FourDotnet.BoogaBooster.Weather.Tests.Testing;
using Xunit;
using WeatherModel = FourDotnet.BoogaBooster.Weather.Domain.Weather;

namespace FourDotnet.BoogaBooster.Weather.Tests;

public sealed class WeatherEventTests
{
    private static readonly TimeSpan Tick = WeatherDefaults.TickInterval;

    [Theory]
    [InlineData(PrecipitationType.Rain)]
    [InlineData(PrecipitationType.Snow)]
    [InlineData(PrecipitationType.Hail)]
    public void Starting_precipitation_activates_the_chosen_type(PrecipitationType type)
    {
        var weather = WeatherModel.CreateDefault();

        weather.StartPrecipitation(type);

        Assert.Equal(WeatherRegime.Precipitation, weather.Regime);
        Assert.Equal(WeatherDefaults.PrecipitationDuration, weather.RemainingEventDuration);
    }

    [Fact]
    public void Starting_precipitation_with_none_is_rejected()
    {
        var weather = WeatherModel.CreateDefault();

        Assert.Throws<DomainValidationException>(() => weather.StartPrecipitation(PrecipitationType.None));
    }

    [Fact]
    public void During_precipitation_temperature_drops_and_wind_picks_up()
    {
        var weather = WeatherModel.CreateDefault();
        weather.StartPrecipitation(PrecipitationType.Rain);

        for (var i = 0; i < 8; i++)
        {
            weather.Advance(Tick, ConstantSampler.Zero);
        }

        var conditions = weather.ToConditionDto();
        Assert.True(conditions.TemperatureCelsius < WeatherDefaults.DefaultTemperatureCelsius,
            $"Expected temperature below default, was {conditions.TemperatureCelsius}.");
        Assert.True(conditions.WindBeaufort > WeatherDefaults.DefaultWindBeaufort,
            $"Expected wind above default, was {conditions.WindBeaufort}.");
        Assert.Equal(PrecipitationType.Rain, conditions.Precipitation);
    }

    [Fact]
    public void Precipitation_stops_after_its_duration_and_reverts_to_defaults()
    {
        var weather = WeatherModel.CreateDefault();
        weather.StartPrecipitation(PrecipitationType.Snow);

        // One large step past the ~15 minute duration expires the event.
        weather.Advance(WeatherDefaults.PrecipitationDuration + Tick, ConstantSampler.Zero);
        Assert.Equal(WeatherRegime.Calm, weather.Regime);

        // Continued calm ticks revert the weather back toward the moderate defaults.
        for (var i = 0; i < 400; i++)
        {
            weather.Advance(Tick, ConstantSampler.Zero);
        }

        var conditions = weather.ToConditionDto();
        Assert.True(Math.Abs(conditions.TemperatureCelsius - WeatherDefaults.DefaultTemperatureCelsius) <= 1d,
            $"Temperature did not revert near the default, was {conditions.TemperatureCelsius}.");
        Assert.True(conditions.WindBeaufort <= WeatherDefaults.DefaultWindBeaufort + 1,
            $"Wind did not ease back near the default, was {conditions.WindBeaufort}.");
        Assert.Equal(PrecipitationType.None, conditions.Precipitation);
    }

    [Fact]
    public void Starting_strong_wind_activates_the_regime_for_its_duration()
    {
        var weather = WeatherModel.CreateDefault();

        weather.StartStrongWind();

        Assert.Equal(WeatherRegime.StrongWind, weather.Regime);
        Assert.Equal(WeatherDefaults.StrongWindDuration, weather.RemainingEventDuration);
    }

    [Fact]
    public void During_strong_wind_wind_climbs_sunshine_fades_and_temperature_drops()
    {
        var weather = WeatherModel.CreateDefault();
        weather.StartStrongWind();

        for (var i = 0; i < 10; i++)
        {
            weather.Advance(Tick, ConstantSampler.Zero);
        }

        var conditions = weather.ToConditionDto();
        Assert.True(conditions.WindBeaufort > WeatherDefaults.DefaultWindBeaufort + 2,
            $"Expected much stronger wind, was {conditions.WindBeaufort}.");
        Assert.True(conditions.SunshinePercent < WeatherDefaults.DefaultSunshinePercent,
            $"Expected reduced sunshine, was {conditions.SunshinePercent}.");
        Assert.True(conditions.TemperatureCelsius < WeatherDefaults.DefaultTemperatureCelsius,
            $"Expected lower temperature, was {conditions.TemperatureCelsius}.");
    }

    [Fact]
    public void Strong_wind_stops_after_its_duration_and_wind_eases_back()
    {
        var weather = WeatherModel.CreateDefault();
        weather.StartStrongWind();

        weather.Advance(WeatherDefaults.StrongWindDuration + Tick, ConstantSampler.Zero);
        Assert.Equal(WeatherRegime.Calm, weather.Regime);

        for (var i = 0; i < 400; i++)
        {
            weather.Advance(Tick, ConstantSampler.Zero);
        }

        var conditions = weather.ToConditionDto();
        Assert.True(conditions.WindBeaufort <= WeatherDefaults.DefaultWindBeaufort + 1,
            $"Wind did not ease back near the default, was {conditions.WindBeaufort}.");
        Assert.True(conditions.SunshinePercent >= WeatherDefaults.DefaultSunshinePercent - 2,
            $"Sunshine did not return near the default, was {conditions.SunshinePercent}.");
        Assert.True(Math.Abs(conditions.TemperatureCelsius - WeatherDefaults.DefaultTemperatureCelsius) <= 1d,
            $"Temperature did not revert near the default, was {conditions.TemperatureCelsius}.");
    }
}
