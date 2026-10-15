using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers over-G nausea episodes (docs/06 §6.6): a gondola that stays at or above max G
/// for more than one second gives each rider +25 nausea, once per episode.
/// </summary>
/// <remarks>
/// The gondola is driven through its real physics step with its brake engaged (the
/// default), so a constant mill speed holds a constant felt G. The riders prefer full
/// intensity, so at or above max G they are at an exact match and the only nausea they
/// can gain is the over-G penalty.
/// </remarks>
public sealed class OverGEpisodeTests
{
    /// <summary>Puts the pivot in a ~7 g field: comfortably above max G.</summary>
    private const double OverGMillOmega = 3d;

    private const string OverGNauseaEpisodesMetric = "boogabooster.ride.over_g.nausea_episodes";

    private static readonly double Dt = TestHelpers.Dt.TotalSeconds;

    private static Gondola LoadedGondola()
    {
        var gondola = new Gondola(0, 0);
        var thrillSeeker = new RiderExperience(70, RideParameters.ExperienceScaleMax, 0);
        gondola.Board(SeatPosition.Left, new Passenger(new PassengerWeight(80), thrillSeeker), TimeSpan.Zero);
        gondola.Board(SeatPosition.Right, new Passenger(new PassengerWeight(70), thrillSeeker), TimeSpan.Zero);
        return gondola;
    }

    private static void Hold(Gondola gondola, double millOmega, double seconds)
    {
        var ticks = (int)Math.Round(seconds / Dt);
        for (var i = 0; i < ticks; i++)
        {
            gondola.AdvancePhysics(millAngle: 0d, millOmega, hubAngle: 0d, hubOmega: 0d, dt: Dt);
        }
    }

    private static double NauseaOf(Gondola gondola, SeatPosition seat) =>
        gondola.GetSeat(seat).Experience!.Value.Nausea;

    [Fact]
    public void The_test_field_is_above_max_g()
    {
        var gondola = LoadedGondola();

        Hold(gondola, OverGMillOmega, seconds: Dt);

        Assert.True(gondola.FeltG >= RideParameters.MaxGForce, $"Felt {gondola.FeltG} g.");
    }

    [Fact]
    public void A_sustained_episode_costs_each_rider_exactly_one_penalty()
    {
        var gondola = LoadedGondola();

        Hold(gondola, OverGMillOmega, seconds: 3);

        Assert.Equal(RideParameters.OverGNauseaPenalty, NauseaOf(gondola, SeatPosition.Left));
        Assert.Equal(RideParameters.OverGNauseaPenalty, NauseaOf(gondola, SeatPosition.Right));
    }

    [Fact]
    public void An_episode_too_short_to_count_costs_nothing()
    {
        var gondola = LoadedGondola();

        Hold(gondola, OverGMillOmega, seconds: 0.5);
        Hold(gondola, millOmega: 0d, seconds: 1);

        Assert.Equal(0d, NauseaOf(gondola, SeatPosition.Left));
    }

    [Fact]
    public void An_episode_only_counts_once_it_has_lasted_the_dwell()
    {
        var gondola = LoadedGondola();

        Hold(gondola, OverGMillOmega, seconds: 0.9);
        Assert.Equal(0d, NauseaOf(gondola, SeatPosition.Left));

        Hold(gondola, OverGMillOmega, seconds: 0.2);
        Assert.Equal(RideParameters.OverGNauseaPenalty, NauseaOf(gondola, SeatPosition.Left));
    }

    [Fact]
    public void Separate_episodes_each_cost_a_penalty()
    {
        var gondola = LoadedGondola();

        Hold(gondola, OverGMillOmega, seconds: 1.5);
        Hold(gondola, millOmega: 0d, seconds: 0.5); // the dip below max G ends the episode
        Hold(gondola, OverGMillOmega, seconds: 1.5);

        Assert.Equal(2 * RideParameters.OverGNauseaPenalty, NauseaOf(gondola, SeatPosition.Left));
        Assert.Equal(2 * RideParameters.OverGNauseaPenalty, NauseaOf(gondola, SeatPosition.Right));
    }

    [Fact]
    public void Short_blips_never_add_up_to_an_episode()
    {
        var gondola = LoadedGondola();

        for (var i = 0; i < 5; i++)
        {
            Hold(gondola, OverGMillOmega, seconds: 0.5);
            Hold(gondola, millOmega: 0d, seconds: 0.1);
        }

        Assert.Equal(0d, NauseaOf(gondola, SeatPosition.Left));
    }

    [Fact]
    public void Each_nausea_episode_is_counted_on_the_shared_meter()
    {
        using var telemetry = new TelemetryRecorder();
        var gondola = LoadedGondola();

        Hold(gondola, OverGMillOmega, seconds: 1.5);
        Hold(gondola, millOmega: 0d, seconds: 0.5);
        Hold(gondola, OverGMillOmega, seconds: 3);

        // The meter is process-wide, so a sibling test may add measurements of its own;
        // this gondola's two episodes must be among them, each counted once and untagged.
        var episodes = telemetry.Measurements(OverGNauseaEpisodesMetric);
        Assert.True(episodes.Count >= 2, $"Expected at least two episodes, saw {episodes.Count}.");
        Assert.All(episodes, measurement =>
        {
            Assert.Equal(1d, measurement.Value);
            Assert.Empty(measurement.Tags);
        });
    }

    [Fact]
    public void An_empty_gondola_rides_out_an_episode_harmlessly()
    {
        var gondola = new Gondola(0, 0);

        Hold(gondola, OverGMillOmega, seconds: 3);

        Assert.Null(gondola.GetSeat(SeatPosition.Left).Experience);
        Assert.True(gondola.FeltG >= RideParameters.MaxGForce);
    }
}
