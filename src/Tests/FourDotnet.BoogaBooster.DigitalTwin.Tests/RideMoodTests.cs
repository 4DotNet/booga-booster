using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers when rider mood changes at the ride level: only while the ride is in
/// motion, and identically for identical rides (<c>docs/06-rider-experience.md</c> §6.7).
/// </summary>
public sealed class RideMoodTests
{
    /// <summary>
    /// A ride with one rider per hub (a balanced load) who prefers a medium ride, so the
    /// cruise intensity falls inside their comfort band and their mood visibly moves.
    /// </summary>
    private static Ride RideWithMediumRiders()
    {
        var ride = Ride.Create();
        for (var hub = 0; hub < RideParameters.HubCount; hub++)
        {
            var profile = new PassengerProfile(new PassengerWeight(75), happiness: 60, preferredRideIntensity: 50, nausea: 0);
            ride.BoardPassenger(hub, 0, SeatPosition.Left, new Passenger(profile), TimeSpan.FromSeconds(1));
        }

        return ride;
    }

    private static IReadOnlyList<Passenger> Riders(Ride ride) =>
        ride.Mill.Hubs
            .SelectMany(hub => hub.Gondolas)
            .SelectMany(gondola => new[] { gondola.GetSeat(SeatPosition.Left), gondola.GetSeat(SeatPosition.Right) })
            .Select(seat => seat.Occupant)
            .OfType<Passenger>()
            .ToArray();

    private static void Run(Ride ride, double seconds)
    {
        var steps = (int)Math.Round(seconds / RideParameters.TimeStep.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            ride.Advance(RideParameters.TimeStep);
        }
    }

    /// <summary>Secures the restraints, starts the ride at full power and runs it.</summary>
    private static void StartAndRun(Ride ride, double seconds)
    {
        Run(ride, seconds: 2);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        ride.SetMainEnginePower(new EnginePower(100));
        ride.SetHubEnginePower(new EnginePower(100));
        Run(ride, seconds);
    }

    [Fact]
    public void Loading_leaves_every_riders_mood_unchanged()
    {
        var ride = RideWithMediumRiders();

        Run(ride, seconds: 60);

        Assert.Equal(RideState.Loading, ride.CurrentState);
        Assert.All(Riders(ride), rider =>
        {
            Assert.Equal(60, rider.Happiness);
            Assert.Equal(0, rider.Nausea);
        });
    }

    [Fact]
    public void A_running_ride_changes_its_riders_mood()
    {
        var ride = RideWithMediumRiders();

        StartAndRun(ride, seconds: 60);

        Assert.Equal(RideState.Started, ride.CurrentState);
        Assert.Contains(Riders(ride), rider => rider.Happiness > 60);
    }

    [Fact]
    public void Replaying_a_ride_reproduces_every_riders_mood()
    {
        var first = RideWithMediumRiders();
        var second = RideWithMediumRiders();

        StartAndRun(first, seconds: 60);
        StartAndRun(second, seconds: 60);
        first.RequestTransition(RideState.Stopping);
        second.RequestTransition(RideState.Stopping);
        Run(first, seconds: 1);
        Run(second, seconds: 1);

        var a = Riders(first);
        var b = Riders(second);
        Assert.Equal(a.Count, b.Count);
        Assert.Equal(a.Select(r => r.Happiness), b.Select(r => r.Happiness));
        Assert.Equal(a.Select(r => r.Nausea), b.Select(r => r.Nausea));
        Assert.Contains(a, rider => rider.Happiness != 60); // the comparison is not vacuous
    }

    [Fact]
    public void Telemetry_summarises_the_riders_mood()
    {
        var ride = Ride.Create();
        ride.BoardGroup(
            [
                new PassengerProfile(new PassengerWeight(75), happiness: 60, preferredRideIntensity: 50, nausea: 0),
                new PassengerProfile(new PassengerWeight(80), happiness: 80, preferredRideIntensity: 100, nausea: 20),
            ],
            () => TimeSpan.Zero);

        var summary = ride.ToTelemetry().RiderExperience;

        Assert.Equal(2, summary.RiderCount);
        Assert.Equal(70, summary.AverageHappiness);
        Assert.Equal(75, summary.AveragePreferredRideIntensity);
        Assert.Equal(10, summary.AverageNausea);
    }

    [Fact]
    public void Telemetry_of_an_empty_ride_has_no_averages()
    {
        var summary = Ride.Create().ToTelemetry().RiderExperience;

        Assert.Equal(0, summary.RiderCount);
        Assert.Null(summary.AverageHappiness);
        Assert.Null(summary.AveragePreferredRideIntensity);
        Assert.Null(summary.AverageNausea);
    }
}
