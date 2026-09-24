using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the mood dynamics of <see cref="Passenger.Experience"/> and
/// <see cref="Passenger.AddNausea"/> (<c>docs/06 §6.2</c>): the match band, the
/// too-intense band, the tamer no-op, and the <c>[0, 1]</c> clamps. Rates are read
/// from <see cref="RideParameters"/>, so retuning never breaks these tests.
/// </summary>
public sealed class PassengerExperienceTests
{
    private const double Tolerance = 1e-9;

    private static Passenger Rider(double preferred, double happiness = 0.5, double nausea = 0) =>
        new(new PassengerWeight(75d), new RiderProfile(preferred, happiness, nausea));

    [Fact]
    public void Experience_MatchedIntensity_GainsHappinessAtTheGainRate()
    {
        // The worked example from the spec: 0.7 to 0.9 over two seconds at the preferred intensity.
        var rider = Rider(preferred: 0.6, happiness: 0.7);

        rider.Experience(intensity: 0.6, dt: 2d);

        Assert.Equal(0.7 + (RideParameters.HappinessGainPerSecond * 2d), rider.Happiness, Tolerance);
        Assert.Equal(0.9, rider.Happiness, Tolerance);
        Assert.Equal(0d, rider.Nausea);
    }

    [Fact]
    public void Experience_TickByTick_AccumulatesAtTheRateTimesTheElapsedSimulatedTime()
    {
        var rider = Rider(preferred: 0.6, happiness: 0.7);
        var steps = (int)Math.Round(2d / TestHelpers.Dt.TotalSeconds);

        for (var i = 0; i < steps; i++)
        {
            rider.Experience(0.6, TestHelpers.Dt.TotalSeconds);
        }

        // The fixed step is a TimeSpan, quantised to 100 ns, so 240 ticks are 1.999992 s
        // rather than 2 s: the expectation is rate × elapsed simulated time, not 0.9 exactly.
        var elapsed = steps * TestHelpers.Dt.TotalSeconds;
        Assert.Equal(0.7 + (RideParameters.HappinessGainPerSecond * elapsed), rider.Happiness, Tolerance);
        Assert.Equal(0.9, rider.Happiness, 1e-5);
    }

    [Theory]
    [InlineData(0.5)]
    [InlineData(0.7)]
    public void Experience_WithinTheToleranceOnEitherSide_CountsAsAMatch(double intensity)
    {
        var rider = Rider(preferred: 0.6, happiness: 0.5);

        rider.Experience(intensity, dt: 1d);

        Assert.True(rider.Happiness > 0.5, $"Intensity {intensity} should match a 0.6 preference.");
        Assert.Equal(0d, rider.Nausea);
    }

    [Fact]
    public void Experience_MatchedIntensity_CapsHappinessAtOne()
    {
        var rider = Rider(preferred: 0.6, happiness: 1d);

        rider.Experience(0.6, dt: 5d);

        Assert.Equal(1d, rider.Happiness);
    }

    [Fact]
    public void Experience_TooIntense_LosesHappinessAndGainsNausea()
    {
        // The worked example from the spec: preference 0.2 on a full-intensity ride for two seconds.
        var rider = Rider(preferred: 0.2, happiness: 0.8, nausea: 0);

        rider.Experience(intensity: 1d, dt: 2d);

        Assert.Equal(0.8 - (RideParameters.HappinessLossPerSecond * 2d), rider.Happiness, Tolerance);
        Assert.Equal(RideParameters.NauseaGainPerSecond * 2d, rider.Nausea, Tolerance);
        Assert.Equal(0.6, rider.Happiness, Tolerance);
        Assert.Equal(0.4, rider.Nausea, Tolerance);
    }

    [Fact]
    public void Experience_JustOutsideTheTolerance_CountsAsTooIntense()
    {
        var rider = Rider(preferred: 0.6, happiness: 0.5, nausea: 0);

        rider.Experience(intensity: 0.75, dt: 1d);

        Assert.True(rider.Happiness < 0.5, "0.75 is more than the tolerance above a 0.6 preference.");
        Assert.True(rider.Nausea > 0d);
    }

    [Fact]
    public void Experience_TooIntense_CapsNauseaAtOneAndFloorsHappinessAtZero()
    {
        var rider = Rider(preferred: 0.1, happiness: 0.05, nausea: 0.95);

        rider.Experience(intensity: 1d, dt: 10d);

        Assert.Equal(1d, rider.Nausea);
        Assert.Equal(0d, rider.Happiness);
    }

    [Fact]
    public void Experience_TamerThanPreferred_LeavesMoodUnchanged()
    {
        var rider = Rider(preferred: 0.9, happiness: 0.5, nausea: 0.1);

        rider.Experience(intensity: 0.3, dt: 10d);

        Assert.Equal(0.5, rider.Happiness);
        Assert.Equal(0.1, rider.Nausea);
    }

    [Fact]
    public void Experience_ZeroTime_ChangesNothing()
    {
        var rider = Rider(preferred: 0.6, happiness: 0.5);

        rider.Experience(0.6, dt: 0d);

        Assert.Equal(0.5, rider.Happiness);
    }

    [Theory]
    [InlineData(-0.1, 1)]
    [InlineData(1.1, 1)]
    [InlineData(double.NaN, 1)]
    [InlineData(0.5, -1)]
    [InlineData(0.5, double.NaN)]
    [InlineData(0.5, double.PositiveInfinity)]
    public void Experience_RejectsAnInvalidIntensityOrTimeStep(double intensity, double dt)
    {
        var rider = Rider(preferred: 0.5);

        Assert.Throws<DomainValidationException>(() => rider.Experience(intensity, dt));
    }

    [Fact]
    public void AddNausea_AddsTheAmount_ClampedToOne()
    {
        var rider = Rider(preferred: 0.5, nausea: 0.1);

        rider.AddNausea(0.5);
        Assert.Equal(0.6, rider.Nausea, Tolerance);

        rider.AddNausea(0.7);
        Assert.Equal(1d, rider.Nausea);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AddNausea_RejectsANegativeOrNonFiniteAmount(double amount)
    {
        var rider = Rider(preferred: 0.5);

        Assert.Throws<DomainValidationException>(() => rider.AddNausea(amount));
    }
}
