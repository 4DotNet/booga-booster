using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>The guarded ride lifecycle state machine: transitions, guards, and automatic settling.</summary>
public sealed class RideStateMachineTests
{
    [Fact]
    public void The_empty_ride_walks_the_full_operator_path()
    {
        var ride = Ride.Create();

        ride.RequestTransition(RideState.Loading);
        Assert.Equal(RideState.Loading, ride.CurrentState);

        ride.RequestTransition(RideState.Safe);
        Assert.Equal(RideState.Safe, ride.CurrentState);

        ride.RequestTransition(RideState.Started);
        Assert.Equal(RideState.Started, ride.CurrentState);

        ride.RequestTransition(RideState.Stopping);
        Assert.Equal(RideState.Stopping, ride.CurrentState);
    }

    [Fact]
    public void An_operator_may_reopen_loading_from_safe()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);

        ride.RequestTransition(RideState.Loading);

        Assert.Equal(RideState.Loading, ride.CurrentState);
    }

    [Theory]
    [InlineData(RideState.Started)]
    [InlineData(RideState.Stopping)]
    [InlineData(RideState.Offloading)]
    [InlineData(RideState.Safe)]
    public void An_undefined_transition_from_idle_is_rejected(RideState target)
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(() => ride.RequestTransition(target));
        Assert.Equal(RideState.Idle, ride.CurrentState);
    }

    [Fact]
    public void An_undefined_transition_from_a_running_ride_is_rejected()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);

        Assert.Throws<DomainValidationException>(() => ride.RequestTransition(RideState.Loading));
        Assert.Equal(RideState.Started, ride.CurrentState);
    }

    [Fact]
    public void Safe_automatically_demotes_to_loading_when_the_load_becomes_unsafe()
    {
        var ride = Ride.Create();
        RideSafetyTests.FillEverySeat(ride, kilograms: 75);
        RideSafetyTests.Advance(ride, seconds: 1d);
        ride.RequestTransition(RideState.Safe);
        Assert.Equal(RideState.Safe, ride.CurrentState);

        // Empty an entire hub so the load becomes badly unbalanced.
        var departed = new List<Passenger>();
        foreach (var gondola in ride.Mill.GetHub(0).Gondolas)
        {
            gondola.ReleaseRestraints();
            gondola.Offload(departed);
        }

        ride.Advance(TestHelpers.Dt);

        Assert.False(ride.Mill.IsBalanced);
        Assert.Equal(RideState.Loading, ride.CurrentState);
    }

    [Fact]
    public void A_controlled_stop_settles_through_offloading_back_to_idle()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);

        ride.RequestTransition(RideState.Stopping);
        Assert.Equal(RideState.Stopping, ride.CurrentState);

        // The empty ride is already at rest, so it settles: Stopping → Offloading → Idle.
        RideSafetyTests.Advance(ride, seconds: 1d);

        Assert.Equal(RideState.Idle, ride.CurrentState);
        Assert.Equal(GondolaBrakeState.Engaged, ride.Mill.GetHub(0).GetGondola(0).Brake);
    }

    [Fact]
    public void Offloading_releases_the_constraints_and_returns_to_idle_when_empty()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(1));
        RideSafetyTests.Advance(ride, seconds: 2d);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);

        ride.RequestTransition(RideState.EmergencyStop);
        Assert.Equal(RideState.EmergencyStop, ride.CurrentState);

        // Advance to a complete rest: constraints release (Offloading), then the rider
        // leaves and the ride returns to Idle.
        var reachedOffloading = AdvanceUntilState(ride, RideState.Offloading, maxSeconds: 5d);
        Assert.True(reachedOffloading);
        Assert.False(ride.ConstraintsLocked);
        Assert.Equal(
            RestraintState.Open,
            ride.Mill.GetHub(0).GetGondola(0).GetSeat(SeatPosition.Left).Restraint);

        var reachedIdle = AdvanceUntilState(ride, RideState.Idle, maxSeconds: 5d);
        Assert.True(reachedIdle);
        Assert.True(ride.Mill.IsEmpty);
    }

    [Theory]
    [InlineData(RideState.Loading)]
    [InlineData(RideState.Safe)]
    [InlineData(RideState.Started)]
    [InlineData(RideState.Stopping)]
    public void Emergency_stop_is_accepted_from_every_active_state(RideState from)
    {
        var ride = RideInState(from);
        ride.SetMainEnginePower(new EnginePower(50));

        ride.RequestTransition(RideState.EmergencyStop);

        Assert.Equal(RideState.EmergencyStop, ride.CurrentState);
        Assert.Equal(0d, ride.Mill.ToTelemetry().PowerWatts);
        Assert.Equal(GondolaBrakeState.Engaged, ride.Mill.GetHub(0).GetGondola(0).Brake);
    }

    [Fact]
    public void Emergency_stop_is_rejected_from_idle()
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(() => ride.RequestTransition(RideState.EmergencyStop));
        Assert.Equal(RideState.Idle, ride.CurrentState);
    }

    [Fact]
    public void Available_transitions_track_the_current_state()
    {
        var ride = Ride.Create();
        Assert.Equal(new[] { RideState.Loading }, ride.AvailableTransitions);

        ride.RequestTransition(RideState.Loading);
        Assert.Contains(RideState.Safe, ride.AvailableTransitions);
        Assert.Contains(RideState.EmergencyStop, ride.AvailableTransitions);

        ride.RequestTransition(RideState.Safe);
        Assert.Contains(RideState.Started, ride.AvailableTransitions);
        Assert.Contains(RideState.Loading, ride.AvailableTransitions);
        Assert.Contains(RideState.EmergencyStop, ride.AvailableTransitions);
        Assert.DoesNotContain(RideState.Idle, ride.AvailableTransitions);

        ride.RequestTransition(RideState.Started);
        Assert.Equal(new[] { RideState.Stopping, RideState.EmergencyStop }, ride.AvailableTransitions);

        ride.RequestTransition(RideState.Stopping);
        Assert.Equal(new[] { RideState.EmergencyStop }, ride.AvailableTransitions);
    }

    [Fact]
    public void A_guarded_transition_is_omitted_from_available_transitions_when_unsafe()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, Passenger.OfWeight(75), TimeSpan.FromSeconds(30));

        // The occupied restraint is still open, so the ride is not safe: Safe is not offered.
        Assert.Equal(RideState.Loading, ride.CurrentState);
        Assert.DoesNotContain(RideState.Safe, ride.AvailableTransitions);
        Assert.Contains(RideState.EmergencyStop, ride.AvailableTransitions);
    }

    /// <summary>Builds an empty ride and drives it into <paramref name="state"/>.</summary>
    private static Ride RideInState(RideState state)
    {
        var ride = Ride.Create();
        if (state == RideState.Loading)
        {
            ride.RequestTransition(RideState.Loading);
            return ride;
        }

        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        if (state == RideState.Safe)
        {
            return ride;
        }

        ride.RequestTransition(RideState.Started);
        if (state == RideState.Started)
        {
            return ride;
        }

        ride.RequestTransition(RideState.Stopping);
        return ride;
    }

    private static bool AdvanceUntilState(Ride ride, RideState target, double maxSeconds)
    {
        var steps = (int)(maxSeconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            if (ride.CurrentState == target)
            {
                return true;
            }

            ride.Advance(TestHelpers.Dt);
        }

        return ride.CurrentState == target;
    }
}
