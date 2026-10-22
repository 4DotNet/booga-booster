using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers a gondola's part in the rider experience: turning the felt G into a ride
/// intensity, applying it to its occupants, and the once-per-episode over-G shock
/// (<c>docs/06-rider-experience.md</c> §6.3, §6.6). The G is driven directly so the
/// rarely reached <c>G_max</c> branch is exercised.
/// </summary>
public sealed class GondolaRiderExperienceTests
{
    private static readonly double Dt = RideParameters.TimeStep.TotalSeconds;

    /// <summary>
    /// A gondola with two riders who prefer the most intense ride, so a ride at
    /// <c>G_max</c> (intensity 100) makes them happy but never over-intense: any
    /// nausea they gain comes from over-G shocks alone.
    /// </summary>
    private static (Gondola Gondola, Passenger Left, Passenger Right) GondolaWithThrillSeekers()
    {
        var gondola = new Gondola(0, 0);
        var left = new Passenger(new PassengerProfile(new PassengerWeight(75), 50, 100, 0));
        var right = new Passenger(new PassengerProfile(new PassengerWeight(80), 50, 100, 0));
        gondola.Board(SeatPosition.Left, left, TimeSpan.Zero);
        gondola.Board(SeatPosition.Right, right, TimeSpan.Zero);
        return (gondola, left, right);
    }

    private static void Feel(Gondola gondola, double feltG, double seconds)
    {
        var steps = (int)Math.Round(seconds / Dt);
        for (var i = 0; i < steps; i++)
        {
            gondola.ExperienceForces(feltG, Dt);
        }
    }

    [Theory]
    [InlineData(2.25, 50)]
    [InlineData(4.5, 100)]
    [InlineData(5.0, 100)]
    [InlineData(1.0, 100d / 4.5d)]
    public void Felt_g_maps_to_a_percentage_of_the_maximum_allowed_g(double feltG, double expectedIntensity)
    {
        Assert.Equal(expectedIntensity, Gondola.IntensityForFeltG(feltG), precision: 10);
    }

    [Fact]
    public void A_gondola_at_rest_feels_one_g()
    {
        var gondola = new Gondola(0, 0);

        Assert.Equal(1d, gondola.FeltG);
        Assert.Equal(100d / 4.5d, gondola.RideIntensity, precision: 10);
    }

    [Fact]
    public void A_moving_gondola_combines_its_horizontal_g_with_gravity()
    {
        var mill = new GreatMill();
        TestHelpers.FillHub(mill.Hubs[0], 75);
        mill.SetPower(new EnginePower(100));
        mill.ReleaseAllGondolaBrakes();

        TestHelpers.StepMill(mill, 20 * 120);

        var gondola = mill.Hubs[0].Gondolas[0];
        var expected = Math.Sqrt((gondola.ForwardG * gondola.ForwardG) + (gondola.LateralG * gondola.LateralG) + 1d);
        Assert.True(gondola.FeltG > 1d);
        Assert.Equal(expected, gondola.FeltG, precision: 10);
        Assert.Equal(Gondola.IntensityForFeltG(expected), gondola.RideIntensity, precision: 10);
    }

    [Fact]
    public void Occupants_experience_the_gondolas_intensity()
    {
        var gondola = new Gondola(0, 0);
        var rider = new Passenger(new PassengerProfile(new PassengerWeight(75), 60, 50, 0));
        gondola.Board(SeatPosition.Left, rider, TimeSpan.Zero);

        Feel(gondola, feltG: 2.25, seconds: 10); // intensity 50 — an exact match

        Assert.Equal(70, rider.Happiness, 0.01);
        Assert.Equal(0, rider.Nausea);
    }

    [Fact]
    public void One_sustained_episode_gives_exactly_one_shock()
    {
        var (gondola, left, right) = GondolaWithThrillSeekers();

        Feel(gondola, feltG: 4.5, seconds: 3);

        Assert.Equal(25, left.Nausea);
        Assert.Equal(25, right.Nausea);
    }

    [Fact]
    public void A_brief_peak_is_not_a_shock()
    {
        var (gondola, left, right) = GondolaWithThrillSeekers();

        Feel(gondola, feltG: 4.6, seconds: 0.5);
        Feel(gondola, feltG: 3.0, seconds: 2);

        Assert.Equal(0, left.Nausea);
        Assert.Equal(0, right.Nausea);
    }

    [Fact]
    public void Exactly_one_second_at_the_limit_is_not_yet_a_shock()
    {
        var (gondola, left, _) = GondolaWithThrillSeekers();

        gondola.ExperienceForces(4.5, 1d);
        Assert.Equal(0, left.Nausea);

        gondola.ExperienceForces(4.5, Dt);
        Assert.Equal(25, left.Nausea);
    }

    [Fact]
    public void Two_episodes_give_two_shocks()
    {
        var (gondola, left, right) = GondolaWithThrillSeekers();

        Feel(gondola, feltG: 4.8, seconds: 1.5);
        Feel(gondola, feltG: 3.0, seconds: 0.5);
        Feel(gondola, feltG: 4.8, seconds: 1.5);

        Assert.Equal(50, left.Nausea);
        Assert.Equal(50, right.Nausea);
    }
}
