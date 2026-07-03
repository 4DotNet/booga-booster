using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

/// <summary>
/// Covers the cross-field validation invariants of the <see cref="Precipitation"/>
/// value object (ADR-0003).
/// </summary>
public sealed class PrecipitationTests
{
    [Fact]
    public void None_HasZeroIntensity()
    {
        Assert.Equal(PrecipitationType.None, Precipitation.None.Type);
        Assert.Equal(0, Precipitation.None.Intensity);
    }

    [Fact]
    public void FallingPrecipitation_HasTypeAndIntensity()
    {
        var rain = new Precipitation(PrecipitationType.Rain, 55);

        Assert.Equal(PrecipitationType.Rain, rain.Type);
        Assert.Equal(55, rain.Intensity);
    }

    [Fact]
    public void UnknownType_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Precipitation((PrecipitationType)999, 10));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void IntensityOutOfRange_Throws(int intensity)
    {
        Assert.Throws<DomainValidationException>(() => new Precipitation(PrecipitationType.Rain, intensity));
    }

    [Fact]
    public void None_WithPositiveIntensity_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Precipitation(PrecipitationType.None, 10));
    }

    [Fact]
    public void FallingPrecipitation_WithZeroIntensity_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Precipitation(PrecipitationType.Rain, 0));
    }
}
