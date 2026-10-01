using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// The passenger-mood spike (tasks 2.8): drives the simulation at fixed mill and hub
/// power for two minutes and measures the peak felt G, so the numbers recorded in
/// <c>docs/06-rider-mood.md</c> §6.6 stay true. If a physics change moves a peak,
/// this fails and the doc must be updated with the new number.
/// </summary>
public sealed class FeltGSpikeTests
{
    private const double RunSeconds = 120d;
    private const double Tolerance = 0.05d;

    [Theory]
    [InlineData(100d, 100d, 32, 6.42d)]
    [InlineData(100d, 100d, 0, 5.99d)]
    [InlineData(75d, 75d, 32, 4.89d)]
    [InlineData(50d, 50d, 32, 3.38d)]
    [InlineData(100d, 0d, 32, 3.67d)]
    [InlineData(0d, 100d, 32, 2.56d)]
    public void PeakFeltG_MatchesTheRecordedSpike(double millPercent, double hubPercent, int riders, double recordedPeak)
    {
        var peak = PeakFeltG(millPercent, hubPercent, riders);

        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Mill {millPercent}%, hubs {hubPercent}%, {riders} riders: peak felt G {peak:0.000} g");

        Assert.Equal(recordedPeak, peak, Tolerance);
    }

    [Fact]
    public void FullPower_ReachesTheSafeLimit()
    {
        Assert.True(PeakFeltG(100d, 100d, riders: 32) > RideParameters.MaxGForce);
    }

    private static double PeakFeltG(double millPercent, double hubPercent, int riders)
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        if (riders > 0)
        {
            ride.BoardGroup([.. Enumerable.Range(1, riders).Select(n => TestHelpers.Guest(n))], () => TimeSpan.Zero);
            ride.Advance(TestHelpers.Dt); // riders secure their restraints
        }

        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        ride.SetMainEnginePower(new EnginePower(millPercent));
        ride.SetHubEnginePower(new EnginePower(hubPercent));

        var peak = 0d;
        var steps = (int)Math.Round(RunSeconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            ride.Advance(TestHelpers.Dt);
            foreach (var hub in ride.Mill.Hubs)
            {
                foreach (var gondola in hub.Gondolas)
                {
                    peak = Math.Max(peak, gondola.FeltG);
                }
            }
        }

        return peak;
    }
}
