using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers how a single passenger's mood evolves on the ride: the comfort-band
/// happiness gain, exponential nausea past the over-intensity threshold, and the
/// over-G shock. Worked values come from <c>docs/06-rider-experience.md</c>.
/// </summary>
public sealed class PassengerMoodTests
{
    private static readonly double Dt = RideParameters.TimeStep.TotalSeconds;

    private static Passenger Rider(double happiness = 70, double preferred = 50, double nausea = 0) =>
        new(new PassengerProfile(new PassengerWeight(75), happiness, preferred, nausea));

    /// <summary>
    /// Rides at a fixed intensity for <paramref name="seconds"/>, in the twin's fixed
    /// physics steps, and returns the time actually simulated. The step is a whole
    /// number of ticks (slightly under 1/120 s), so whole-second rides fall a few
    /// microseconds short; worked values are therefore compared with a tolerance.
    /// </summary>
    private static double Ride(Passenger passenger, double intensity, double seconds)
    {
        var steps = (int)Math.Round(seconds / Dt);
        for (var i = 0; i < steps; i++)
        {
            passenger.ExperienceRide(intensity, Dt);
        }

        return steps * Dt;
    }

    private const double WorkedValueTolerance = 0.01;

    [Fact]
    public void A_passenger_carries_the_profile_they_boarded_with()
    {
        var passenger = Rider(happiness: 40, preferred: 90, nausea: 5);

        Assert.Equal(75, passenger.Weight.Kilograms);
        Assert.Equal(40, passenger.Happiness);
        Assert.Equal(90, passenger.PreferredRideIntensity);
        Assert.Equal(5, passenger.Nausea);
    }

    [Fact]
    public void A_passenger_boarded_by_weight_has_the_default_mood()
    {
        var passenger = Passenger.OfWeight(88);

        Assert.Equal(88, passenger.Weight.Kilograms);
        Assert.Equal(75, passenger.Happiness);
        Assert.Equal(75, passenger.PreferredRideIntensity);
        Assert.Equal(0, passenger.Nausea);
    }

    [Fact]
    public void A_50_percent_rider_gains_about_10_points_in_10_seconds_at_50_percent()
    {
        var passenger = Rider(happiness: 70, preferred: 50);

        Ride(passenger, intensity: 50, seconds: 10);

        Assert.Equal(80, passenger.Happiness, WorkedValueTolerance);
    }

    [Fact]
    public void A_100_percent_rider_gains_faster_at_100_than_at_90_percent()
    {
        var atLimit = Rider(happiness: 50, preferred: 100);
        var belowLimit = Rider(happiness: 50, preferred: 100);

        Ride(atLimit, intensity: 100, seconds: 10);
        Ride(belowLimit, intensity: 90, seconds: 10);

        Assert.True(atLimit.Happiness > 50);
        Assert.True(belowLimit.Happiness > 50);
        Assert.True(atLimit.Happiness > belowLimit.Happiness);
        Assert.Equal(60, atLimit.Happiness, WorkedValueTolerance);
        Assert.Equal(50 + (10d / 3d), belowLimit.Happiness, WorkedValueTolerance);
    }

    [Theory]
    [InlineData(50)]  // far below the preference
    [InlineData(85)]  // exactly at the band's edge
    public void A_mismatched_ride_brings_no_joy(double intensity)
    {
        var passenger = Rider(happiness: 70, preferred: 100);

        Ride(passenger, intensity, seconds: 10);

        Assert.Equal(70, passenger.Happiness);
    }

    [Fact]
    public void Happiness_is_capped_at_100()
    {
        var passenger = Rider(happiness: 99.5, preferred: 75);

        Ride(passenger, intensity: 75, seconds: 10);

        Assert.Equal(100, passenger.Happiness);
    }

    [Theory]
    [InlineData(20, 3)]
    [InlineData(40, 15)]
    [InlineData(60, 63)]
    public void Prolonged_over_intensity_escalates_nausea_exponentially(double seconds, double expected)
    {
        var passenger = Rider(preferred: 50, nausea: 0);

        Ride(passenger, intensity: 85, seconds);

        Assert.Equal(expected, passenger.Nausea, WorkedValueTolerance);
    }

    [Fact]
    public void Nausea_does_not_depend_on_the_step_size()
    {
        var stepped = Rider(preferred: 50);
        var single = Rider(preferred: 50);

        var elapsed = Ride(stepped, intensity: 85, seconds: 40);
        single.ExperienceRide(85, elapsed);

        Assert.Equal(single.Nausea, stepped.Nausea, precision: 6);
    }

    [Fact]
    public void Nausea_is_capped_at_100()
    {
        var passenger = Rider(preferred: 50);

        Ride(passenger, intensity: 100, seconds: 90);

        Assert.Equal(100, passenger.Nausea);
    }

    [Fact]
    public void Below_the_threshold_nausea_holds()
    {
        var passenger = Rider(preferred: 50, nausea: 10);

        Ride(passenger, intensity: 75, seconds: 30);

        Assert.Equal(10, passenger.Nausea);
    }

    [Fact]
    public void Exactly_at_the_threshold_nausea_grows()
    {
        var passenger = Rider(preferred: 50, nausea: 0);

        passenger.ExperienceRide(80, 10);

        Assert.Equal(1, passenger.Nausea, precision: 10);
    }

    [Fact]
    public void An_over_g_shock_adds_25_nausea_capped_at_100()
    {
        var passenger = Rider(nausea: 60);

        passenger.SufferOverGShock();
        Assert.Equal(85, passenger.Nausea);

        passenger.SufferOverGShock();
        Assert.Equal(100, passenger.Nausea);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(100.1, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(50, 0)]
    [InlineData(50, -1)]
    [InlineData(50, double.PositiveInfinity)]
    public void Experiencing_an_invalid_ride_is_rejected(double intensity, double seconds)
    {
        var passenger = Rider();

        Assert.Throws<DomainValidationException>(() => passenger.ExperienceRide(intensity, seconds));
    }

    [Fact]
    public void A_passenger_needs_a_profile()
    {
        Assert.Throws<ArgumentNullException>(() => new Passenger(null!));
    }
}
