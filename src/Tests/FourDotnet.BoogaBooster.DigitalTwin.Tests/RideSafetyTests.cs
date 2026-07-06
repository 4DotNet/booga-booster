using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>The safety interlock that guards the loadingsafestarted transitions.</summary>
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
    public void Boarding_moves_the_ride_to_loading_and_flags_the_unsecured_restraint()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(10));

        Assert.Equal(RideState.Loading, ride.CurrentState);
        Assert.Equal(RideSafetyReason.UnsecuredRestraint, ride.SafetyReason);
        Assert.False(ride.IsSafeToStart);
    }

    [Fact]
    public void The_ride_cannot_reach_safe_while_a_restraint_is_unsecured()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(10));

        Assert.Throws<DomainValidationException>(() => ride.RequestTransition(RideState.Safe));
        Assert.Equal(RideState.Loading, ride.CurrentState);
    }

    [Fact]
    public void Once_every_occupied_restraint_is_secured_the_ride_can_reach_safe_and_start()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(1));

        Advance(ride, seconds: 2d);

        // No auto-promotion: the ride stays Loading until the operator declares it safe.
        Assert.Equal(RideState.Loading, ride.CurrentState);
        Assert.True(ride.IsSafeToStart);

        ride.RequestTransition(RideState.Safe);
        Assert.Equal(RideState.Safe, ride.CurrentState);

        ride.RequestTransition(RideState.Started);
        Assert.Equal(RideState.Started, ride.CurrentState);
        // Starting locks the safety constraints and releases the gondola brakes.
        Assert.True(ride.ConstraintsLocked);
        Assert.Equal(GondolaBrakeState.Released, ride.Mill.GetHub(0).GetGondola(0).Brake);
    }

    [Fact]
    public void An_unbalanced_load_blocks_reaching_safe_even_when_secured()
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
        Assert.Throws<DomainValidationException>(() => ride.RequestTransition(RideState.Safe));
    }

    [Fact]
    public void An_overloaded_ride_blocks_reaching_safe_even_when_secured_and_balanced()
    {
        var ride = Ride.Create();
        // Fill every seat symmetrically with the heaviest passengers: balanced and
        // secured, but 32 × 130 kg = 4160 kg — over the 3200 kg maximum load.
        FillEverySeat(ride, kilograms: 130);

        Advance(ride, seconds: 1d);

        Assert.True(ride.Mill.IsBalanced);
        Assert.Equal(RideSafetyReason.Overloaded, ride.SafetyReason);
        Assert.False(ride.IsSafeToStart);
        Assert.Throws<DomainValidationException>(() => ride.RequestTransition(RideState.Safe));
    }

    [Fact]
    public void Passengers_cannot_board_while_the_ride_is_running()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(1));
        Advance(ride, seconds: 2d);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);

        Assert.Throws<DomainValidationException>(() =>
            ride.BoardPassenger(1, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.Zero));
    }

    internal static void FillEverySeat(Ride ride, double kilograms)
    {
        foreach (var hub in ride.Mill.Hubs)
        {
            foreach (var gondola in hub.Gondolas)
            {
                ride.BoardPassenger(hub.Index, gondola.Index, SeatPosition.Left, Passenger.OfWeight(kilograms), TimeSpan.Zero);
                ride.BoardPassenger(hub.Index, gondola.Index, SeatPosition.Right, Passenger.OfWeight(kilograms), TimeSpan.Zero);
            }
        }
    }

    internal static void Advance(Ride ride, double seconds)
    {
        var steps = (int)(seconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }
    }
}
