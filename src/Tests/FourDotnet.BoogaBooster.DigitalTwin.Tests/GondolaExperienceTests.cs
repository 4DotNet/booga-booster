using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers how a <see cref="Gondola"/> turns its felt G into its riders' experience
/// (docs/06-passenger-experience.md §6.3–6.6): the experienced intensity, the ride step
/// applied to each occupant, and the once-per-episode sustained-max-G penalty.
/// </summary>
/// <remarks>
/// The gondola is held with its brake engaged under a still mill, so the felt G is
/// exactly the hub's centrifugal field at the pivot, <c>ω²·r / g</c> — which lets each
/// test dial in a precise G through the real physics step.
/// </remarks>
public sealed class GondolaExperienceTests
{
    private static readonly double Dt = TestHelpers.Dt.TotalSeconds;

    /// <summary>A rider whose preference matches maximum intensity, so the ride itself adds no nausea.</summary>
    private static Passenger ThrillSeeker() =>
        new(new PassengerWeight(75d), new PassengerExperience(50d, 100d, 0d));

    /// <summary>The hub speed that makes a braked gondola feel <paramref name="g"/> horizontally.</summary>
    private static double HubOmegaFor(double g) => Math.Sqrt(g * RideParameters.Gravity / RideParameters.HubArmLength);

    /// <summary>
    /// Holds the gondola at <paramref name="g"/> for whole physics steps covering about
    /// <paramref name="seconds"/>, and returns the simulated time actually elapsed (the
    /// fixed step is a whole number of ticks, so it is not exactly 1/120 s).
    /// </summary>
    private static double Hold(Gondola gondola, double g, double seconds)
    {
        var hubOmega = HubOmegaFor(g);
        var steps = (int)Math.Round(seconds / Dt);
        for (var i = 0; i < steps; i++)
        {
            gondola.AdvancePhysics(millAngle: 0d, millOmega: 0d, hubAngle: 0d, hubOmega: hubOmega, dt: Dt);
        }

        return steps * Dt;
    }

    private static Gondola BoardedGondola(Passenger left, Passenger? right = null)
    {
        var gondola = new Gondola(0, 0);
        gondola.Board(SeatPosition.Left, left, TimeSpan.Zero);
        if (right is not null)
        {
            gondola.Board(SeatPosition.Right, right, TimeSpan.Zero);
        }

        return gondola;
    }

    // --- Experienced intensity (§6.3) ---

    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(2.25d, 50d)]
    [InlineData(RideParameters.MaxGForce, 100d)]
    [InlineData(5d, 100d)]
    public void The_experienced_intensity_is_felt_g_as_a_percentage_of_the_maximum_capped_at_100(double g, double expected)
    {
        var gondola = new Gondola(0, 0);

        Hold(gondola, g, seconds: Dt);

        Assert.Equal(expected, gondola.ExperiencedIntensity, precision: 9);
    }

    [Fact]
    public void Both_riders_ride_the_gondolas_intensity()
    {
        var left = new Passenger(new PassengerWeight(70d), new PassengerExperience(60d, 50d, 0d));
        var right = new Passenger(new PassengerWeight(90d), new PassengerExperience(60d, 50d, 0d));
        var gondola = BoardedGondola(left, right);

        var elapsed = Hold(gondola, g: 2.25d, seconds: 10d);

        // A perfect match (intensity 50 for a 50 rider) gains at the peak rate throughout.
        var expected = 60d + (RideParameters.HappinessGainRate * elapsed);
        Assert.Equal(expected, left.Experience.Happiness, precision: 9);
        Assert.Equal(expected, right.Experience.Happiness, precision: 9);
    }

    [Fact]
    public void Riding_far_beyond_the_preference_makes_the_rider_nauseous()
    {
        var timid = new Passenger(new PassengerWeight(70d), new PassengerExperience(60d, 50d, 0d));
        var gondola = BoardedGondola(timid);

        Hold(gondola, g: 4d, seconds: 2d); // intensity ≈ 89, a 39-point excess, below max G

        Assert.True(timid.Experience.Nausea > 0d);
    }

    // --- Sustained max-G episodes (§6.6) ---

    [Fact]
    public void A_brief_peak_above_max_g_adds_no_penalty()
    {
        var rider = ThrillSeeker();
        var gondola = BoardedGondola(rider);

        Hold(gondola, g: 5d, seconds: 0.8d);
        Hold(gondola, g: 0d, seconds: 1d);

        Assert.Equal(0d, rider.Experience.Nausea);
    }

    [Fact]
    public void One_long_episode_adds_the_penalty_exactly_once()
    {
        var rider = ThrillSeeker();
        var gondola = BoardedGondola(rider);

        Hold(gondola, g: 5d, seconds: 3d);

        Assert.Equal(RideParameters.MaxGNauseaPenalty, rider.Experience.Nausea);
    }

    [Fact]
    public void Two_separate_episodes_each_add_the_penalty()
    {
        var rider = ThrillSeeker();
        var gondola = BoardedGondola(rider);

        Hold(gondola, g: 5d, seconds: 1.5d);
        Hold(gondola, g: 0d, seconds: 0.5d);
        Hold(gondola, g: 5d, seconds: 1.5d);

        Assert.Equal(2d * RideParameters.MaxGNauseaPenalty, rider.Experience.Nausea);
    }

    [Fact]
    public void A_dip_below_max_g_restarts_the_episode_count()
    {
        var rider = ThrillSeeker();
        var gondola = BoardedGondola(rider);

        // Two 0.8 s peaks add up to more than a second, but neither is an episode on its own.
        Hold(gondola, g: 5d, seconds: 0.8d);
        Hold(gondola, g: 0d, seconds: Dt);
        Hold(gondola, g: 5d, seconds: 0.8d);

        Assert.Equal(0d, rider.Experience.Nausea);
    }

    [Fact]
    public void Both_riders_of_the_gondola_suffer_the_penalty()
    {
        var left = ThrillSeeker();
        var right = ThrillSeeker();
        var gondola = BoardedGondola(left, right);

        Hold(gondola, g: 5d, seconds: 2d);

        Assert.Equal(RideParameters.MaxGNauseaPenalty, left.Experience.Nausea);
        Assert.Equal(RideParameters.MaxGNauseaPenalty, right.Experience.Nausea);
    }

    [Fact]
    public void An_empty_seat_is_untouched_by_an_episode()
    {
        var rider = ThrillSeeker();
        var gondola = BoardedGondola(rider);

        Hold(gondola, g: 5d, seconds: 3d);

        Assert.Equal(RideParameters.MaxGNauseaPenalty, rider.Experience.Nausea);
        Assert.False(gondola.GetSeat(SeatPosition.Right).IsOccupied);
        Assert.Equal(1, gondola.OccupiedSeatCount);
    }

    [Fact]
    public void An_empty_gondola_tracks_intensity_without_riders()
    {
        var gondola = new Gondola(0, 0);

        Hold(gondola, g: 5d, seconds: 3d);

        Assert.True(gondola.IsEmpty);
        Assert.Equal(100d, gondola.ExperiencedIntensity, precision: 9);
    }
}
