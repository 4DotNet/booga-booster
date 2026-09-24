using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the <see cref="RiderProfile"/> value object (validated on construction,
/// ADR-0003) and how a <see cref="Passenger"/> is built from it — including the
/// neutral profile <see cref="Passenger.OfWeight"/> falls back to.
/// </summary>
public sealed class RiderProfileTests
{
    [Theory]
    [InlineData(0.1, 0, 0)]
    [InlineData(0.4, 0.7, 0)]
    [InlineData(1, 1, 1)]
    [InlineData(0.55, 0.5, 0.25)]
    public void RiderProfile_AcceptsValuesInRange(double preferred, double happiness, double nausea)
    {
        var profile = new RiderProfile(preferred, happiness, nausea);

        Assert.Equal(preferred, profile.PreferredIntensity);
        Assert.Equal(happiness, profile.Happiness);
        Assert.Equal(nausea, profile.Nausea);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.0999)]
    [InlineData(1.0001)]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RiderProfile_RejectsAnOutOfRangePreferredIntensity(double preferred)
    {
        Assert.Throws<DomainValidationException>(() => new RiderProfile(preferred, 0.5, 0));
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(1.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void RiderProfile_RejectsAnOutOfRangeHappiness(double happiness)
    {
        Assert.Throws<DomainValidationException>(() => new RiderProfile(0.5, happiness, 0));
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(1.0001)]
    [InlineData(double.NaN)]
    [InlineData(double.NegativeInfinity)]
    public void RiderProfile_RejectsAnOutOfRangeNausea(double nausea)
    {
        Assert.Throws<DomainValidationException>(() => new RiderProfile(0.5, 0.5, nausea));
    }

    [Fact]
    public void RiderProfile_Neutral_UsesTheRideParameterDefaults()
    {
        var neutral = RiderProfile.Neutral;

        Assert.Equal(RideParameters.DefaultPreferredIntensity, neutral.PreferredIntensity);
        Assert.Equal(RideParameters.DefaultHappiness, neutral.Happiness);
        Assert.Equal(0d, neutral.Nausea);
    }

    [Fact]
    public void Passenger_CreatedWithAProfile_ReportsItsWeightAndMood()
    {
        var passenger = new Passenger(new PassengerWeight(80d), new RiderProfile(0.4, 0.7, 0));

        Assert.Equal(80d, passenger.Weight.Kilograms);
        Assert.Equal(0.4, passenger.PreferredIntensity);
        Assert.Equal(0.7, passenger.Happiness);
        Assert.Equal(0d, passenger.Nausea);
    }

    [Fact]
    public void Passenger_WithAnInvalidProfile_IsRejectedBeforeItExists()
    {
        // Preferred intensity 0 is below the floor: the value object refuses it, so no
        // passenger can ever carry it.
        Assert.Throws<DomainValidationException>(
            () => new Passenger(new PassengerWeight(80d), new RiderProfile(0, 0.7, 0)));
    }

    [Fact]
    public void Passenger_RequiresBothAWeightAndAProfile()
    {
        Assert.Throws<ArgumentNullException>(() => new Passenger(null!, RiderProfile.Neutral));
        Assert.Throws<ArgumentNullException>(() => new Passenger(new PassengerWeight(80d), null!));
    }

    [Fact]
    public void Passenger_OfWeight_UsesTheNeutralProfile()
    {
        var passenger = Passenger.OfWeight(75d);

        Assert.Equal(75d, passenger.Weight.Kilograms);
        Assert.Equal(RideParameters.DefaultPreferredIntensity, passenger.PreferredIntensity);
        Assert.Equal(RideParameters.DefaultHappiness, passenger.Happiness);
        Assert.Equal(0d, passenger.Nausea);
    }
}
