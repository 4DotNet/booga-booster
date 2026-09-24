using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the offload histograms (design D10, ADR-0009): every passenger leaving
/// the ride records one final-happiness and one final-nausea measurement, untagged,
/// so the series stays a single distribution and no measurement identifies a person.
/// </summary>
/// <remarks>
/// The meter is process-wide, so the riders carry distinctive moods that no sibling
/// test produces, and the assertions look for exactly those values.
/// </remarks>
public sealed class RiderOffloadTelemetryTests : IDisposable
{
    private const string HappinessHistogram = "boogabooster.ride.rider.happiness.final";
    private const string NauseaHistogram = "boogabooster.ride.rider.nausea.final";

    private static readonly double[] Happiness = [0.6123, 0.6234, 0.6345];
    private static readonly double[] Nausea = [0.1123, 0.1234, 0.1345];

    private readonly TelemetryRecorder _telemetry = new();

    public void Dispose() => _telemetry.Dispose();

    [Fact]
    public void Offloading_RecordsEachLeavingRidersFinalMood_Untagged()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 21));
        store.BoardGroup(
        [
            Rider(Happiness[0], Nausea[0]),
            Rider(Happiness[1], Nausea[1]),
            Rider(Happiness[2], Nausea[2]),
        ]);

        RunAFullCycle(store);

        for (var i = 0; i < Happiness.Length; i++)
        {
            var happiness = Assert.Single(_telemetry.Measurements(HappinessHistogram), m => m.Value == Happiness[i]);
            var nausea = Assert.Single(_telemetry.Measurements(NauseaHistogram), m => m.Value == Nausea[i]);
            Assert.Empty(happiness.Tags);
            Assert.Empty(nausea.Tags);
        }
    }

    [Fact]
    public void AdvancingWithoutAnOffload_RecordsNoFinalMood()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 22));
        store.BoardGroup([Rider(0.7777, 0.2222)]);

        Advance(store, seconds: 2d);

        Assert.DoesNotContain(_telemetry.Measurements(HappinessHistogram), m => m.Value == 0.7777);
        Assert.DoesNotContain(_telemetry.Measurements(NauseaHistogram), m => m.Value == 0.2222);
    }

    /// <summary>
    /// A neutral-preference rider on an unpowered ride feels intensity 0, which is
    /// tamer than they like, so their mood is exactly what they boarded with when they leave.
    /// </summary>
    private static Passenger Rider(double happiness, double nausea) =>
        new(new PassengerWeight(60d), new RiderProfile(RideParameters.DefaultPreferredIntensity, happiness, nausea));

    /// <summary>Loading, Safe, Started, Stopping, Offloading and back to Idle, on an unpowered ride.</summary>
    private static void RunAFullCycle(RideStore store)
    {
        Advance(store, seconds: RideParameters.MaxRestraintCloseDelay.TotalSeconds + 1d);
        store.RequestStateTransition(RideState.Safe);
        store.StartRide();
        Advance(store, seconds: 1d);
        store.StopRide();
        Advance(store, seconds: 1d);
        Assert.Equal(RideState.Idle, store.CurrentState);
    }

    private static void Advance(RideStore store, double seconds)
    {
        var steps = (int)Math.Round(seconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            store.Advance(TestHelpers.Dt);
        }
    }
}
