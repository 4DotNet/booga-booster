using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the gondola side of the rider experience (<c>docs/06 §6.1, §6.3</c>): the
/// intensity mapping from felt horizontal G, and the sustained-G nausea latch.
/// </summary>
/// <remarks>
/// G is driven through a known kinematic state rather than the motor curve: for hub 0,
/// gondola 0 at mill and hub angle 0 with the hub still, the pivot sits at
/// <c>r = L_mill + L_hub</c> on the +x axis, so the felt horizontal specific force is
/// exactly <c>ω_mill² · r</c>. The brake stays engaged so the gondola's own swing adds
/// nothing and the step size is free — <see cref="Gondola.AdvancePhysics"/> updates the
/// G-forces and the riders every call regardless.
/// </remarks>
public sealed class GondolaRiderExperienceTests
{
    private const double Tolerance = 1e-9;
    private const double PivotRadius = RideParameters.MillArmLength + RideParameters.HubArmLength;

    /// <summary>A step size that is exactly representable, so accumulated seconds are exact.</summary>
    private const double Step = 0.5d;

    /// <summary>The mill speed that produces <paramref name="g"/> of horizontal G at the test pivot.</summary>
    private static double OmegaForG(double g) => Math.Sqrt(g * RideParameters.Gravity / PivotRadius);

    private static Gondola GondolaWith(Passenger? left = null, Passenger? right = null)
    {
        var gondola = new Gondola(0, 0);
        if (left is not null)
        {
            gondola.Board(SeatPosition.Left, left, TimeSpan.Zero);
        }

        if (right is not null)
        {
            gondola.Board(SeatPosition.Right, right, TimeSpan.Zero);
        }

        return gondola;
    }

    private static void HoldAtG(Gondola gondola, double g, double seconds)
    {
        var omega = OmegaForG(g);
        var steps = (int)Math.Round(seconds / Step);
        for (var i = 0; i < steps; i++)
        {
            gondola.AdvancePhysics(millAngle: 0d, millOmega: omega, hubAngle: 0d, hubOmega: 0d, dt: Step);
        }
    }

    private static void DropBelowLimit(Gondola gondola) =>
        gondola.AdvancePhysics(millAngle: 0d, millOmega: 0d, hubAngle: 0d, hubOmega: 0d, dt: Step);

    private static Passenger Rider(double preferred, double happiness = 0.5, double nausea = 0) =>
        new(new PassengerWeight(75d), new RiderProfile(preferred, happiness, nausea));

    // --- Intensity (§6.1) ---

    [Fact]
    public void AtRest_IntensityIsZero_AndTheGondolaIsNotAtTheLimit()
    {
        var gondola = GondolaWith(Rider(0.5));

        gondola.AdvancePhysics(0d, 0d, 0d, 0d, TestHelpers.Dt.TotalSeconds);

        Assert.Equal(0d, gondola.HorizontalG, Tolerance);
        Assert.Equal(0d, gondola.Intensity, Tolerance);
        Assert.False(gondola.IsAtGLimit);
    }

    [Fact]
    public void HorizontalG_IsTheMagnitudeOfTheLateralAndForwardComponents()
    {
        var gondola = GondolaWith();

        gondola.AdvancePhysics(0d, OmegaForG(2.25), 0d, 0d, TestHelpers.Dt.TotalSeconds);

        var expected = Math.Sqrt((gondola.LateralG * gondola.LateralG) + (gondola.ForwardG * gondola.ForwardG));
        Assert.Equal(expected, gondola.HorizontalG, Tolerance);
        Assert.Equal(2.25, gondola.HorizontalG, Tolerance);
    }

    [Fact]
    public void IntensityScalesWithHorizontalG_HalfTheMaximumIsHalfIntensity()
    {
        var gondola = GondolaWith();

        gondola.AdvancePhysics(0d, OmegaForG(RideParameters.MaxGForce / 2d), 0d, 0d, TestHelpers.Dt.TotalSeconds);

        Assert.Equal(0.5, gondola.Intensity, Tolerance);
        Assert.False(gondola.IsAtGLimit);
    }

    [Fact]
    public void IntensitySaturatesAtTheLimit_AndTheGondolaReportsBeingAtIt()
    {
        var gondola = GondolaWith();

        gondola.AdvancePhysics(0d, OmegaForG(5d), 0d, 0d, TestHelpers.Dt.TotalSeconds);

        Assert.Equal(5d, gondola.HorizontalG, Tolerance);
        Assert.Equal(1d, gondola.Intensity);
        Assert.True(gondola.IsAtGLimit);
    }

    [Fact]
    public void Intensity_IsRecomputedEveryTick_SoItFollowsTheRideDown()
    {
        var gondola = GondolaWith();
        gondola.AdvancePhysics(0d, OmegaForG(5d), 0d, 0d, Step);
        Assert.True(gondola.IsAtGLimit);

        DropBelowLimit(gondola);

        Assert.Equal(0d, gondola.Intensity, Tolerance);
        Assert.False(gondola.IsAtGLimit);
    }

    // --- The riders feel the intensity (§6.2) ---

    [Fact]
    public void SeatedRiders_ExperienceTheGondolaIntensityEachTick()
    {
        // At 2.7 g the intensity is 0.6: a 0.6-preference rider is matched, a 0.2-preference rider is overwhelmed.
        var matched = Rider(preferred: 0.6, happiness: 0.7);
        var overwhelmed = Rider(preferred: 0.2, happiness: 0.8);
        var gondola = GondolaWith(matched, overwhelmed);

        HoldAtG(gondola, g: 0.6 * RideParameters.MaxGForce, seconds: 2d);

        Assert.Equal(0.6, gondola.Intensity, Tolerance);
        Assert.Equal(0.9, matched.Happiness, Tolerance);
        Assert.Equal(0d, matched.Nausea);
        Assert.Equal(0.6, overwhelmed.Happiness, Tolerance);
        Assert.Equal(0.4, overwhelmed.Nausea, Tolerance);
    }

    [Fact]
    public void AnEmptyGondola_AdvancesWithoutError()
    {
        var gondola = GondolaWith();

        HoldAtG(gondola, g: 5d, seconds: 3d);

        Assert.True(gondola.IsAtGLimit);
        Assert.Equal(3d, gondola.SecondsAtGLimit, Tolerance);
    }

    // --- The sustained-G latch (§6.3) ---
    // A preference-1 rider is matched at the limit (delta 0), so the continuous path
    // touches only happiness and any nausea change is the penalty alone.

    [Fact]
    public void TwoSecondsAtTheLimit_AddsThePenaltyOnce()
    {
        var rider = Rider(preferred: 1d, nausea: 0.1);
        var gondola = GondolaWith(rider);

        HoldAtG(gondola, g: 5d, seconds: RideParameters.SustainedGLimitSeconds);

        Assert.Equal(0.1 + RideParameters.SustainedGLimitNauseaPenalty, rider.Nausea, Tolerance);
        Assert.Equal(RideParameters.SustainedGLimitSeconds, gondola.SecondsAtGLimit, Tolerance);
    }

    [Fact]
    public void JustShortOfTwoSeconds_NoPenaltyYet()
    {
        var rider = Rider(preferred: 1d, nausea: 0.1);
        var gondola = GondolaWith(rider);

        HoldAtG(gondola, g: 5d, seconds: RideParameters.SustainedGLimitSeconds - Step);

        Assert.Equal(0.1, rider.Nausea);
    }

    [Fact]
    public void FiveSecondsAtTheLimit_StillAppliesThePenaltyExactlyOnce()
    {
        var rider = Rider(preferred: 1d, nausea: 0.1);
        var gondola = GondolaWith(rider);

        HoldAtG(gondola, g: 5d, seconds: 5d);

        Assert.Equal(0.1 + RideParameters.SustainedGLimitNauseaPenalty, rider.Nausea, Tolerance);
    }

    [Fact]
    public void AShortExcursion_OneSecondTwice_DoesNotTriggerThePenalty()
    {
        var rider = Rider(preferred: 1d, nausea: 0.1);
        var gondola = GondolaWith(rider);

        HoldAtG(gondola, g: 5d, seconds: 1d);
        DropBelowLimit(gondola);
        Assert.Equal(0d, gondola.SecondsAtGLimit);
        HoldAtG(gondola, g: 5d, seconds: 1d);

        Assert.Equal(0.1, rider.Nausea);
    }

    [Fact]
    public void DroppingBelowTheLimit_ReArmsThePenalty()
    {
        var rider = Rider(preferred: 1d, nausea: 0d);
        var gondola = GondolaWith(rider);

        HoldAtG(gondola, g: 5d, seconds: RideParameters.SustainedGLimitSeconds);
        DropBelowLimit(gondola);
        HoldAtG(gondola, g: 5d, seconds: RideParameters.SustainedGLimitSeconds);

        Assert.Equal(2d * RideParameters.SustainedGLimitNauseaPenalty, rider.Nausea, Tolerance);
    }

    [Fact]
    public void ThrillSeekers_AreNotExempt()
    {
        var thrillSeeker = Rider(preferred: RiderProfile.MaxPreferredIntensity, nausea: 0d);
        var gondola = GondolaWith(thrillSeeker);

        HoldAtG(gondola, g: 5d, seconds: RideParameters.SustainedGLimitSeconds);

        Assert.Equal(RideParameters.SustainedGLimitNauseaPenalty, thrillSeeker.Nausea, Tolerance);
        Assert.True(thrillSeeker.Happiness > 0.5, "The matched thrill seeker still gets happier meanwhile.");
    }

    [Fact]
    public void ThePenalty_ReachesBothSeats()
    {
        var left = Rider(preferred: 1d, nausea: 0d);
        var right = Rider(preferred: 1d, nausea: 0.2);
        var gondola = GondolaWith(left, right);

        HoldAtG(gondola, g: 5d, seconds: RideParameters.SustainedGLimitSeconds);

        Assert.Equal(RideParameters.SustainedGLimitNauseaPenalty, left.Nausea, Tolerance);
        Assert.Equal(0.2 + RideParameters.SustainedGLimitNauseaPenalty, right.Nausea, Tolerance);
    }

    [Fact]
    public void ThePenalty_IsClampedAtOne()
    {
        var rider = Rider(preferred: 1d, nausea: 0.8);
        var gondola = GondolaWith(rider);

        HoldAtG(gondola, g: 5d, seconds: RideParameters.SustainedGLimitSeconds);

        Assert.Equal(1d, rider.Nausea);
    }

    // --- Offloading hands back the riders ---

    [Fact]
    public void Offload_ReturnsTheLeavingPassengers_WithTheirFinalMood()
    {
        var left = Rider(preferred: 0.6, happiness: 0.7);
        var right = Rider(preferred: 0.2, happiness: 0.8);
        var gondola = GondolaWith(left, right);
        HoldAtG(gondola, g: 0.6 * RideParameters.MaxGForce, seconds: 2d);
        gondola.ReleaseRestraints();

        var departed = new List<Passenger>();
        gondola.Offload(departed);

        Assert.Equal([left, right], departed);
        Assert.Equal(0.9, departed[0].Happiness, Tolerance);
        Assert.Equal(0.4, departed[1].Nausea, Tolerance);
        Assert.True(gondola.IsEmpty);
    }

    [Fact]
    public void Offload_OfAnEmptyGondola_AddsNobody()
    {
        var gondola = GondolaWith();
        var departed = new List<Passenger>();

        gondola.Offload(departed);

        Assert.Empty(departed);
    }

    [Fact]
    public void Offload_RequiresACollector()
    {
        Assert.Throws<ArgumentNullException>(() => GondolaWith().Offload(null!));
    }
}
