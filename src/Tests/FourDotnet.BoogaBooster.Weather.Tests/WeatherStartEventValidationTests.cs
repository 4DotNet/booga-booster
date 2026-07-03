using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using WeatherModel = FourDotnet.BoogaBooster.Weather.Domain.Weather;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

/// <summary>
/// Covers the guard clauses when triggering a precipitation event directly on the
/// <see cref="WeatherModel"/> aggregate.
/// </summary>
public sealed class WeatherStartEventValidationTests
{
    [Fact]
    public void StartPrecipitation_None_Throws()
    {
        var weather = WeatherModel.CreateDefault();

        Assert.Throws<DomainValidationException>(() => weather.StartPrecipitation(PrecipitationType.None));
    }

    [Fact]
    public void StartPrecipitation_UnknownType_Throws()
    {
        var weather = WeatherModel.CreateDefault();

        Assert.Throws<DomainValidationException>(() => weather.StartPrecipitation((PrecipitationType)999));
    }

    [Fact]
    public void StartPrecipitation_Rain_BecomesActiveWithFallingRain()
    {
        var weather = WeatherModel.CreateDefault();

        weather.StartPrecipitation(PrecipitationType.Rain);

        Assert.Equal(WeatherRegime.Precipitation, weather.Regime);
        Assert.Equal(PrecipitationType.Rain, weather.Precipitation.Type);
        Assert.True(weather.Precipitation.Intensity > 0);
        Assert.True(weather.RemainingEventDuration > TimeSpan.Zero);
    }
}
