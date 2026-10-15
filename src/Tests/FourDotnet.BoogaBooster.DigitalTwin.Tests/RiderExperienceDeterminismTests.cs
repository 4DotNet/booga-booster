using Bogus;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// The experience dynamics run inside the fixed-step tick (docs/06 §6.7), so they
/// inherit its determinism: two identically seeded simulations driven by identical
/// operator inputs must produce identical happiness and nausea for every passenger at
/// every tick.
/// </summary>
public sealed class RiderExperienceDeterminismTests
{
    private const int Seed = 20261015;
    private const int Ticks = 20 * 120; // twenty seconds of riding

    /// <summary>
    /// A full ride of equal-weight riders (so it is balanced and safe to start) whose
    /// experiences are drawn from a seeded source, started at full power.
    /// </summary>
    private static Ride SeededRunningRide()
    {
        var random = new Randomizer(Seed);
        var ride = Ride.Create();
        foreach (var hub in ride.Mill.Hubs)
        {
            foreach (var gondola in hub.Gondolas)
            {
                foreach (var seat in new[] { SeatPosition.Left, SeatPosition.Right })
                {
                    var experience = new RiderExperience(
                        random.Double(60, 90),
                        random.Double(0, 100),
                        random.Double(0, 20));
                    ride.BoardPassenger(
                        hub.Index,
                        gondola.Index,
                        seat,
                        new Passenger(new PassengerWeight(RideParameters.DefaultPassengerKg), experience),
                        TimeSpan.Zero);
                }
            }
        }

        ride.Advance(TestHelpers.Dt); // riders secure their restraints
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        ride.SetMainEnginePower(new EnginePower(100));
        ride.SetHubEnginePower(new EnginePower(100));
        return ride;
    }

    private static RiderExperience[] Riders(Ride ride) =>
    [
        .. ride.Mill.Hubs
            .SelectMany(hub => hub.Gondolas)
            .SelectMany(gondola => new[] { gondola.GetSeat(SeatPosition.Left), gondola.GetSeat(SeatPosition.Right) })
            .Select(seat => seat.Experience!.Value),
    ];

    [Fact]
    public void Replayed_simulations_produce_identical_experience_at_every_tick()
    {
        var first = SeededRunningRide();
        var second = SeededRunningRide();
        var start = Riders(first);

        for (var tick = 0; tick < Ticks; tick++)
        {
            first.Advance(TestHelpers.Dt);
            second.Advance(TestHelpers.Dt);

            Assert.Equal(Riders(first), Riders(second));
        }

        // Guard against a vacuous pass: the ride must actually have moved the riders.
        Assert.NotEqual(start, Riders(first));
    }
}
