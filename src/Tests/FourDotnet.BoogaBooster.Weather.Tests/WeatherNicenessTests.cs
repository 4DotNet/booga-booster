using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Domain;
using FourDotnet.BoogaBooster.Weather.Tests.Testing;
using Xunit;
using WeatherModel = FourDotnet.BoogaBooster.Weather.Domain.Weather;

namespace FourDotnet.BoogaBooster.Weather.Tests;

public sealed class WeatherNicenessTests
{
    [Fact]
    public void Moderate_default_weather_is_perfectly_nice()
    {
        var niceness = WeatherNiceness.Calculate(
            WeatherDefaults.DefaultTemperatureCelsius,
            WeatherDefaults.DefaultWindBeaufort,
            PrecipitationType.None,
            precipitationIntensity: 0);

        Assert.Equal(1f, niceness);
    }

    [Fact]
    public void A_severe_storm_is_not_nice_at_all()
    {
        var niceness = WeatherNiceness.Calculate(
            temperatureCelsius: 14,
            windBeaufort: 11,
            PrecipitationType.None,
            precipitationIntensity: 0);

        Assert.Equal(0f, niceness);
    }

    [Fact]
    public void Heavy_precipitation_is_not_nice_at_all()
    {
        var niceness = WeatherNiceness.Calculate(
            temperatureCelsius: 12,
            windBeaufort: 4,
            PrecipitationType.Hail,
            precipitationIntensity: 100);

        Assert.Equal(0f, niceness);
    }

    [Fact]
    public void Niceness_stays_within_the_unit_range()
    {
        var weather = WeatherModel.CreateDefault();
        var sampler = new RandomWeatherSampler(seed: 55);

        // Push through calm, precipitation, and strong-wind regimes.
        for (var i = 0; i < 50; i++)
        {
            weather.Advance(WeatherDefaults.TickInterval, sampler);
        }

        weather.StartPrecipitation(PrecipitationType.Snow);
        for (var i = 0; i < 50; i++)
        {
            weather.Advance(WeatherDefaults.TickInterval, sampler);
            Assert.InRange(weather.ToConditionDto().NiceWeather, 0f, 1f);
        }

        weather.StartStrongWind();
        for (var i = 0; i < 50; i++)
        {
            weather.Advance(WeatherDefaults.TickInterval, sampler);
            Assert.InRange(weather.ToConditionDto().NiceWeather, 0f, 1f);
        }
    }

    [Fact]
    public void Nice_weather_indicator_is_higher_when_calm_than_during_a_storm()
    {
        var calm = WeatherModel.CreateDefault();

        var stormy = WeatherModel.CreateDefault();
        stormy.StartStrongWind();
        for (var i = 0; i < 20; i++)
        {
            stormy.Advance(WeatherDefaults.TickInterval, ConstantSampler.Zero);
        }

        Assert.True(calm.ToConditionDto().NiceWeather > stormy.ToConditionDto().NiceWeather);
    }
}
