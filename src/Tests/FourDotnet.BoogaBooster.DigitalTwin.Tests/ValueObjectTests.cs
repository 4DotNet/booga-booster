using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData(30)]
    [InlineData(75)]
    [InlineData(130)]
    [InlineData(150)]
    public void PassengerWeight_accepts_values_in_range(double kilograms)
    {
        var weight = new PassengerWeight(kilograms);
        Assert.Equal(kilograms, weight.Kilograms);
    }

    [Theory]
    [InlineData(29.9)]
    [InlineData(150.1)]
    [InlineData(0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void PassengerWeight_rejects_out_of_range_values(double kilograms)
    {
        Assert.Throws<DomainValidationException>(() => new PassengerWeight(kilograms));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void EnginePower_accepts_0_to_100_percent(double percent)
    {
        var power = new EnginePower(percent);
        Assert.Equal(percent, power.Percent);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public void EnginePower_rejects_values_outside_0_to_100(double percent)
    {
        Assert.Throws<DomainValidationException>(() => new EnginePower(percent));
    }

    [Fact]
    public void EnginePower_converts_percentage_to_watts()
    {
        var power = new EnginePower(50);
        Assert.Equal(0.5d, power.Fraction);
        Assert.Equal(45_000d, power.ToWatts(90_000d));
    }

    [Fact]
    public void EnginePower_off_is_zero()
    {
        Assert.Equal(0d, EnginePower.Off.Percent);
    }
}
