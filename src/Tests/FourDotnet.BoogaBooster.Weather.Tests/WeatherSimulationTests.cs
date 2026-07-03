using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Domain;
using FourDotnet.BoogaBooster.Weather.Tests.Testing;
using Xunit;
using WeatherModel = FourDotnet.BoogaBooster.Weather.Domain.Weather;

namespace FourDotnet.BoogaBooster.Weather.Tests;

public sealed class WeatherSimulationTests
{
    private static readonly TimeSpan Tick = WeatherDefaults.TickInterval;

    [Fact]
    public void Fresh_weather_reports_the_moderate_defaults_and_calm_regime()
    {
        var weather = WeatherModel.CreateDefault();

        var conditions = weather.ToConditionDto();

        Assert.Equal(WeatherDefaults.DefaultTemperatureCelsius, conditions.TemperatureCelsius);
        Assert.Equal(WeatherDefaults.DefaultWindBeaufort, conditions.WindBeaufort);
        Assert.Equal(WeatherDefaults.DefaultSunshinePercent, conditions.SunshinePercent);
        Assert.Equal(PrecipitationType.None, conditions.Precipitation);
        Assert.Equal(WeatherRegime.Calm, conditions.Regime);
    }

    [Fact]
    public void Calm_readings_change_only_a_little_each_tick()
    {
        var weather = WeatherModel.CreateDefault();
        var sampler = new RandomWeatherSampler(seed: 1234);

        var previous = weather.ToConditionDto();
        for (var i = 0; i < 200; i++)
        {
            weather.Advance(Tick, sampler);
            var current = weather.ToConditionDto();

            Assert.True(Math.Abs(current.TemperatureCelsius - previous.TemperatureCelsius) <= 3d,
                $"Temperature jumped from {previous.TemperatureCelsius} to {current.TemperatureCelsius}.");
            Assert.True(Math.Abs(current.WindBeaufort - previous.WindBeaufort) <= 2,
                $"Wind jumped from {previous.WindBeaufort} to {current.WindBeaufort}.");

            previous = current;
        }
    }

    [Fact]
    public void Calm_readings_stay_in_a_bounded_band_around_the_defaults()
    {
        var weather = WeatherModel.CreateDefault();
        var sampler = new RandomWeatherSampler(seed: 99);

        for (var i = 0; i < 1000; i++)
        {
            weather.Advance(Tick, sampler);
            var conditions = weather.ToConditionDto();

            Assert.InRange(conditions.TemperatureCelsius, WeatherDefaults.DefaultTemperatureCelsius - 8d, WeatherDefaults.DefaultTemperatureCelsius + 8d);
            Assert.InRange(conditions.WindBeaufort, Wind.MinBeaufort, WeatherDefaults.DefaultWindBeaufort + 4);
            Assert.InRange(conditions.SunshinePercent, Sunshine.MinPercent, Sunshine.MaxPercent);
            Assert.Equal(PrecipitationType.None, conditions.Precipitation);
        }
    }

    [Fact]
    public void Readings_never_leave_valid_bounds_even_with_extreme_noise()
    {
        var weather = WeatherModel.CreateDefault();
        // A sampler pinned to +1 pushes every reading up as hard as the model allows.
        var sampler = new ConstantSampler(1d);

        for (var i = 0; i < 500; i++)
        {
            weather.Advance(Tick, sampler);
            var conditions = weather.ToConditionDto();

            Assert.InRange(conditions.TemperatureCelsius, Temperature.MinCelsius, Temperature.MaxCelsius);
            Assert.InRange(conditions.WindBeaufort, Wind.MinBeaufort, Wind.MaxBeaufort);
            Assert.InRange(conditions.SunshinePercent, Sunshine.MinPercent, Sunshine.MaxPercent);
        }
    }

    [Fact]
    public void Same_seed_and_same_steps_produce_the_same_state_sequence()
    {
        var first = Run(new RandomWeatherSampler(seed: 2026));
        var second = Run(new RandomWeatherSampler(seed: 2026));

        Assert.Equal(first, second);

        static List<WeatherConditionDto> Run(IWeatherSampler sampler)
        {
            var weather = WeatherModel.CreateDefault();
            var states = new List<WeatherConditionDto>();
            for (var i = 0; i < 300; i++)
            {
                weather.Advance(Tick, sampler);
                states.Add(weather.ToConditionDto());
            }

            return states;
        }
    }

    [Fact]
    public void A_no_op_tick_reports_no_change()
    {
        // Starting exactly at the calm target with no noise, nothing moves.
        var weather = WeatherModel.CreateDefault();

        var changed = weather.Advance(Tick, ConstantSampler.Zero);

        Assert.False(changed);
    }
}
