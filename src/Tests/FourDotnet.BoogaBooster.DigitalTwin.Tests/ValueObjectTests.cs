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
    [InlineData(0, 0, 0)]
    [InlineData(40, 90, 0)]
    [InlineData(100, 100, 100)]
    public void PassengerProfile_accepts_mood_scores_in_range(double happiness, double intensity, double nausea)
    {
        var profile = new PassengerProfile(new PassengerWeight(80), happiness, intensity, nausea);

        Assert.Equal(80, profile.Weight.Kilograms);
        Assert.Equal(happiness, profile.Happiness);
        Assert.Equal(intensity, profile.PreferredRideIntensity);
        Assert.Equal(nausea, profile.Nausea);
    }

    [Theory]
    [InlineData(101, 75, 0)]
    [InlineData(-0.1, 75, 0)]
    [InlineData(75, 100.5, 0)]
    [InlineData(75, -1, 0)]
    [InlineData(75, 75, 100.1)]
    [InlineData(75, 75, -1)]
    [InlineData(75, double.NaN, 0)]
    public void PassengerProfile_rejects_out_of_range_mood_scores(double happiness, double intensity, double nausea)
    {
        Assert.Throws<DomainValidationException>(
            () => new PassengerProfile(new PassengerWeight(80), happiness, intensity, nausea));
    }

    [Fact]
    public void PassengerProfile_requires_a_weight()
    {
        Assert.Throws<ArgumentNullException>(() => new PassengerProfile(null!, 75, 75, 0));
    }

    [Fact]
    public void PassengerProfile_with_default_mood_is_75_75_0()
    {
        var profile = PassengerProfile.WithDefaultMood(new PassengerWeight(60));

        Assert.Equal(60, profile.Weight.Kilograms);
        Assert.Equal(75, profile.Happiness);
        Assert.Equal(75, profile.PreferredRideIntensity);
        Assert.Equal(0, profile.Nausea);
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
