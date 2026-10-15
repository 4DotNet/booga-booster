using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the on-ride experience dynamics (docs/06 §6.4–6.5): happiness rises when the
/// ride intensity matches a rider's preference, nausea grows exponentially when the
/// ride is far too intense, and the gondola applies both every physics tick.
/// </summary>
public sealed class RiderDynamicsTests
{
    private const double Precision = 1e-9;
    private static readonly double Dt = TestHelpers.Dt.TotalSeconds;

    /// <summary>Rides <paramref name="seconds"/> at a constant intensity, one fixed step at a time.</summary>
    private static RiderExperience Ride(RiderExperience experience, double intensity, double seconds)
    {
        var ticks = (int)Math.Round(seconds / Dt);
        for (var i = 0; i < ticks; i++)
        {
            experience = experience.AfterRiding(intensity, Dt);
        }

        return experience;
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public void A_rider_at_their_preferred_intensity_grows_significantly_happier(double preference)
    {
        var after = Ride(new RiderExperience(70, preference, 0), intensity: preference, seconds: 5);

        // Full rate at an exact match: 2 points/s for 5 s (TimeStep is 1/120 s truncated to
        // whole 100 ns ticks, hence the tolerance).
        Assert.Equal(80d, after.Happiness, 1e-3);
    }

    [Fact]
    public void An_exact_match_raises_happiness_faster_than_a_ten_point_offset()
    {
        var exact = Ride(new RiderExperience(70, 60, 0), intensity: 60, seconds: 5);
        var offset = Ride(new RiderExperience(70, 60, 0), intensity: 70, seconds: 5);

        Assert.True(exact.Happiness > offset.Happiness);
        Assert.True(offset.Happiness > 70d, "A 10-point offset is still inside the match band.");
    }

    [Theory]
    [InlineData(100, 40)] // far too tame
    [InlineData(50, 70)]  // 20 points too intense — outside the band, below the nausea threshold
    public void A_mismatched_ride_leaves_happiness_unchanged(double preference, double intensity)
    {
        var after = Ride(new RiderExperience(70, preference, 0), intensity, seconds: 5);

        Assert.Equal(70d, after.Happiness);
    }

    [Fact]
    public void Happiness_is_held_at_the_top_of_the_scale()
    {
        var after = Ride(new RiderExperience(99, 60, 0), intensity: 60, seconds: 5);

        Assert.Equal(RideParameters.ExperienceScaleMax, after.Happiness);
    }

    [Fact]
    public void A_ride_far_too_intense_grows_nausea_exponentially_from_zero()
    {
        var experience = new RiderExperience(70, 50, 0);
        var readings = new List<double> { experience.Nausea };
        for (var interval = 0; interval < 4; interval++)
        {
            experience = Ride(experience, intensity: 85, seconds: 5);
            readings.Add(experience.Nausea);
        }

        Assert.True(readings[1] > 0d, "Nausea must start growing from zero.");
        for (var i = 2; i < readings.Count; i++)
        {
            var previousGain = readings[i - 1] - readings[i - 2];
            var gain = readings[i] - readings[i - 1];
            Assert.True(gain > previousGain, $"Interval {i} added {gain}, not more than the {previousGain} before it.");
        }

        // dN/dt = k (N + s) ⇒ N(t) = (N₀ + s)·e^{kt} − s; explicit Euler at the fixed step stays within 0.25 % of it.
        Assert.Equal(Math.Exp(RideParameters.NauseaGrowthRate * 20d) - 1d, readings[^1], 0.05);
    }

    [Fact]
    public void Nausea_starts_exactly_at_the_excess_threshold()
    {
        var after = new RiderExperience(70, 50, 0).AfterRiding(intensity: 50 + RideParameters.NauseaIntensityExcess, Dt);

        Assert.True(after.Nausea > 0d);
    }

    [Fact]
    public void An_excess_below_the_threshold_adds_no_nausea()
    {
        var after = Ride(new RiderExperience(70, 50, 0), intensity: 75, seconds: 10);

        Assert.Equal(0d, after.Nausea);
    }

    [Fact]
    public void Nausea_is_held_at_the_top_of_the_scale()
    {
        var after = Ride(new RiderExperience(70, 0, 0), intensity: 100, seconds: 60);

        Assert.Equal(RideParameters.ExperienceScaleMax, after.Nausea);
    }

    [Fact]
    public void Riding_never_changes_the_preference()
    {
        var after = Ride(new RiderExperience(70, 50, 0), intensity: 90, seconds: 5);

        Assert.Equal(50d, after.PreferredRideIntensity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    public void Riding_rejects_a_non_positive_step(double dt)
    {
        Assert.Throws<DomainValidationException>(() => new RiderExperience(70, 50, 0).AfterRiding(50, dt));
    }

    [Fact]
    public void An_empty_seat_has_nobody_to_react()
    {
        var seat = new Seat(SeatPosition.Left);

        seat.AdvanceExperience(intensity: 50, Dt);

        Assert.Null(seat.Experience);
    }

    [Fact]
    public void A_seated_passenger_reacts_to_the_intensity()
    {
        var seat = new Seat(SeatPosition.Left);
        seat.Board(new Passenger(new PassengerWeight(80), new RiderExperience(70, 50, 0)), TimeSpan.Zero);

        seat.AdvanceExperience(intensity: 50, Dt);

        Assert.Equal(70d + (RideParameters.HappinessGainRate * Dt), seat.Experience!.Value.Happiness, Precision);
    }

    [Fact]
    public void A_gondola_advances_both_riders_from_its_intensity_every_physics_tick()
    {
        // The brake is engaged by default, so at a constant field the felt G — and with
        // it the intensity — is the same on every tick.
        var gondola = new Gondola(0, 0);
        gondola.Board(SeatPosition.Left, new Passenger(new PassengerWeight(80), new RiderExperience(60, 20, 0)), TimeSpan.Zero);
        gondola.Board(SeatPosition.Right, new Passenger(new PassengerWeight(70), new RiderExperience(60, 0, 0)), TimeSpan.Zero);

        var expectedLeft = new RiderExperience(60, 20, 0);
        var expectedRight = new RiderExperience(60, 0, 0);
        for (var i = 0; i < 240; i++)
        {
            gondola.AdvancePhysics(millAngle: 0d, millOmega: 1.2d, hubAngle: 0d, hubOmega: 0d, dt: Dt);
            expectedLeft = expectedLeft.AfterRiding(gondola.Intensity, Dt);
            expectedRight = expectedRight.AfterRiding(gondola.Intensity, Dt);
        }

        Assert.Equal(expectedLeft, gondola.GetSeat(SeatPosition.Left).Experience);
        Assert.Equal(expectedRight, gondola.GetSeat(SeatPosition.Right).Experience);
        Assert.NotEqual(new RiderExperience(60, 20, 0), expectedLeft);
    }
}
