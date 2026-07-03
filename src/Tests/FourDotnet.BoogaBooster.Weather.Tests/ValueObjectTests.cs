using FourDotnet.BoogaBooster.Core.Domain;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData(-50)]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(60)]
    public void Temperature_accepts_values_in_range(double celsius)
    {
        var temperature = new Temperature(celsius);
        Assert.Equal(celsius, temperature.Celsius);
    }

    [Theory]
    [InlineData(-50.1)]
    [InlineData(60.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Temperature_rejects_out_of_range_values(double celsius)
    {
        Assert.Throws<DomainValidationException>(() => new Temperature(celsius));
    }

    [Fact]
    public void Temperature_is_quantized_to_one_decimal()
    {
        var temperature = new Temperature(19.96);
        Assert.Equal(20.0, temperature.Celsius);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(12)]
    public void Wind_accepts_beaufort_0_to_12(int beaufort)
    {
        var wind = new Wind(beaufort);
        Assert.Equal(beaufort, wind.Beaufort);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public void Wind_rejects_values_outside_the_beaufort_scale(int beaufort)
    {
        Assert.Throws<DomainValidationException>(() => new Wind(beaufort));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Sunshine_rejects_values_outside_0_to_100(int percent)
    {
        Assert.Throws<DomainValidationException>(() => new Sunshine(percent));
    }

    [Fact]
    public void Precipitation_none_must_have_zero_intensity()
    {
        Assert.Throws<DomainValidationException>(() => new Precipitation(PrecipitationType.None, 5));
    }

    [Fact]
    public void Precipitation_falling_must_have_positive_intensity()
    {
        Assert.Throws<DomainValidationException>(() => new Precipitation(PrecipitationType.Rain, 0));
    }

    [Fact]
    public void Precipitation_accepts_a_falling_type_with_intensity()
    {
        var precipitation = new Precipitation(PrecipitationType.Snow, 40);
        Assert.Equal(PrecipitationType.Snow, precipitation.Type);
        Assert.Equal(40, precipitation.Intensity);
    }
}
