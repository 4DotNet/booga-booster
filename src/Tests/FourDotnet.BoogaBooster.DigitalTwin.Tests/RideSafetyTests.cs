using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class RideSafetyTests
{
    [Fact]
    public void A_new_ride_is_idle_and_empty()
    {
        var ride = Ride.Create();

        Assert.Equal(RideState.Idle, ride.CurrentState);
        Assert.Equal(RideSafetyReason.None, ride.SafetyReason);
        Assert.True(ride.IsSafeToStart);
    }

    [Fact]
    public void Boarding_moves_the_ride_to_boarding_and_flags_the_unsecured_restraint()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(10));

        Assert.Equal(RideState.Boarding, ride.CurrentState);
        Assert.Equal(RideSafetyReason.UnsecuredRestraint, ride.SafetyReason);
        Assert.False(ride.IsSafeToStart);
    }

    [Fact]
    public void The_ride_cannot_start_while_a_restraint_is_unsecured()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(10));

        Assert.Throws<DomainValidationException>(() => ride.Start());
    }

    [Fact]
    public void Once_every_occupied_restraint_is_secured_the_ride_becomes_ready_and_starts()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(1));

        Advance(ride, seconds: 2d);

        Assert.Equal(RideState.Ready, ride.CurrentState);
        Assert.True(ride.IsSafeToStart);

        ride.Start();

        Assert.Equal(RideState.Running, ride.CurrentState);
        // Starting releases the gondola brakes so they can swing freely.
        Assert.Equal(GondolaBrakeState.Released, ride.Mill.GetHub(0).GetGondola(0).Brake);
    }

    [Fact]
    public void An_unbalanced_load_blocks_the_start_even_when_secured()
    {
        var ride = Ride.Create();
        // Load only hub 0 heavily — secured, but badly unbalanced.
        foreach (var gondola in ride.Mill.GetHub(0).Gondolas)
        {
            ride.BoardPassenger(0, gondola.Index, SeatPosition.Left, Passenger.OfWeight(130), TimeSpan.Zero);
            ride.BoardPassenger(0, gondola.Index, SeatPosition.Right, Passenger.OfWeight(130), TimeSpan.Zero);
        }

        Advance(ride, seconds: 1d);

        Assert.Equal(RideSafetyReason.UnbalancedLoad, ride.SafetyReason);
        Assert.Throws<DomainValidationException>(() => ride.Start());
    }

    [Fact]
    public void An_overloaded_ride_blocks_the_start_even_when_secured_and_balanced()
    {
        var ride = Ride.Create();
        // Fill every seat symmetrically with the heaviest passengers: balanced and
        // secured, but 32 × 130 kg = 4160 kg — over the 3200 kg maximum load.
        foreach (var hub in ride.Mill.Hubs)
        {
            foreach (var gondola in hub.Gondolas)
            {
                ride.BoardPassenger(hub.Index, gondola.Index, SeatPosition.Left, Passenger.OfWeight(130), TimeSpan.Zero);
                ride.BoardPassenger(hub.Index, gondola.Index, SeatPosition.Right, Passenger.OfWeight(130), TimeSpan.Zero);
            }
        }

        Advance(ride, seconds: 1d);

        Assert.True(ride.Mill.IsBalanced);
        Assert.Equal(RideSafetyReason.Overloaded, ride.SafetyReason);
        Assert.False(ride.IsSafeToStart);
        Assert.Throws<DomainValidationException>(() => ride.Start());
    }

    [Fact]
    public void Stopping_ramps_down_and_engages_the_brakes_at_rest()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(1));
        Advance(ride, seconds: 2d);
        ride.Start();

        ride.Stop();
        Assert.Equal(RideState.Stopping, ride.CurrentState);

        // With no power applied the ride is already at rest, so it settles to idle.
        Advance(ride, seconds: 1d);
        Assert.Equal(RideState.Idle, ride.CurrentState);
        Assert.Equal(GondolaBrakeState.Engaged, ride.Mill.GetHub(0).GetGondola(0).Brake);
    }

    [Fact]
    public void Passengers_cannot_board_while_the_ride_is_running()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(1));
        Advance(ride, seconds: 2d);
        ride.Start();

        Assert.Throws<DomainValidationException>(() =>
            ride.BoardPassenger(1, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.Zero));
    }

    private static void Advance(Ride ride, double seconds)
    {
        var steps = (int)(seconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }
    }
}
