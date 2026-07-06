using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// The engine brake: a toggle that cuts drive power and applies a strong braking
/// torque so the mill and hubs come to a complete rest within a couple of seconds,
/// both on operator command and automatically while the ride is stopping.
/// </summary>
public sealed class EngineBrakeTests
{
    /// <summary>The required feel: a braking ride is at rest within a couple of seconds.</summary>
    private const double MaxBrakingStopSeconds = 3d;

    [Fact]
    public void An_engaged_brake_stops_a_spinning_mill_and_hubs_within_a_couple_of_seconds()
    {
        var mill = SpunUpMill();

        mill.EngageBrakes();
        var seconds = StepsToRest(mill) * TestHelpers.Dt.TotalSeconds;

        Assert.True(mill.IsAtRest, "The braking ride never reached rest.");
        Assert.True(
            seconds <= MaxBrakingStopSeconds,
            $"A braking ride took {seconds:0.0}s to stop — longer than the {MaxBrakingStopSeconds}s ceiling.");
    }

    [Fact]
    public void A_released_brake_applies_no_braking_torque()
    {
        var mill = SpunUpMill();

        // Cut power but leave the brake released: the ride coasts on ordinary friction,
        // which is far slower than a braking stop (the coast-down floor is ~8s).
        mill.CutAllPower();
        TestHelpers.StepMill(mill, steps: (int)(MaxBrakingStopSeconds / TestHelpers.Dt.TotalSeconds));

        Assert.False(mill.BrakesEngaged);
        Assert.False(
            mill.IsAtRest,
            "A released ride stopped within the braking window — it must only coast on ordinary friction.");
    }

    [Fact]
    public void The_brake_never_spins_a_body_up_from_rest()
    {
        var mill = new GreatMill();

        mill.EngageBrakes();
        TestHelpers.StepMill(mill, steps: 120 * 3);

        Assert.True(mill.IsAtRest, "The engaged brake must never drive a body that is at rest.");
        Assert.Equal(0d, mill.AngularVelocity);
    }

    [Fact]
    public void A_new_mill_has_the_brake_released()
    {
        var mill = new GreatMill();

        Assert.False(mill.BrakesEngaged);
    }

    [Fact]
    public void Engaging_the_brake_cuts_all_drive_power()
    {
        var ride = Ride.Create();
        ride.SetMainEnginePower(new EnginePower(80));
        ride.SetHubEnginePower(new EnginePower(60));

        ride.SetEngineBrakes(true);

        Assert.True(ride.Mill.BrakesEngaged);
        Assert.Equal(0d, ride.Mill.ToTelemetry().PowerWatts);
        Assert.All(ride.Mill.Hubs, h => Assert.Equal(0d, h.ToTelemetry().PowerWatts));
    }

    [Fact]
    public void Releasing_the_brake_does_not_restore_power()
    {
        var ride = Ride.Create();
        ride.SetMainEnginePower(new EnginePower(80));
        ride.SetEngineBrakes(true);

        ride.SetEngineBrakes(false);

        Assert.False(ride.Mill.BrakesEngaged);
        Assert.Equal(0d, ride.Mill.ToTelemetry().PowerWatts);
    }

    [Fact]
    public void Engaging_an_already_engaged_brake_is_idempotent()
    {
        var ride = Ride.Create();
        ride.SetEngineBrakes(true);

        ride.SetEngineBrakes(true);

        Assert.True(ride.Mill.BrakesEngaged);
        Assert.Equal(0d, ride.Mill.ToTelemetry().PowerWatts);
    }

    [Fact]
    public void Entering_stopping_engages_the_brake_and_cuts_power()
    {
        var ride = RunningRide();
        ride.SetMainEnginePower(new EnginePower(50));

        ride.RequestTransition(RideState.Stopping);

        Assert.True(ride.Mill.BrakesEngaged);
        Assert.Equal(0d, ride.Mill.ToTelemetry().PowerWatts);
    }

    [Fact]
    public void Entering_emergency_stop_engages_the_brake_and_cuts_power()
    {
        var ride = RunningRide();
        ride.SetMainEnginePower(new EnginePower(50));

        ride.RequestTransition(RideState.EmergencyStop);

        Assert.True(ride.Mill.BrakesEngaged);
        Assert.Equal(0d, ride.Mill.ToTelemetry().PowerWatts);
    }

    [Fact]
    public void Entering_started_releases_the_brake()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.SetEngineBrakes(true);
        Assert.True(ride.Mill.BrakesEngaged);

        ride.RequestTransition(RideState.Started);

        Assert.False(ride.Mill.BrakesEngaged);
    }

    [Fact]
    public void A_stopping_ride_releases_the_brake_once_it_has_offloaded()
    {
        var ride = RunningRide();

        ride.RequestTransition(RideState.Stopping);
        Assert.True(ride.Mill.BrakesEngaged);

        // The empty ride is already at rest, so it settles: Stopping → Offloading → Idle,
        // and the engine brake is released on entering Offloading.
        RideSafetyTests.Advance(ride, seconds: 1d);

        Assert.Equal(RideState.Idle, ride.CurrentState);
        Assert.False(ride.Mill.BrakesEngaged);
    }

    [Fact]
    public void Telemetry_reports_the_brake_state()
    {
        var ride = Ride.Create();
        Assert.False(ride.ToTelemetry().BrakesEngaged);

        ride.SetEngineBrakes(true);
        Assert.True(ride.ToTelemetry().BrakesEngaged);

        ride.SetEngineBrakes(false);
        Assert.False(ride.ToTelemetry().BrakesEngaged);
    }

    /// <summary>A mill (and its hubs) driven to full cruise on full power.</summary>
    private static GreatMill SpunUpMill()
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100));
        mill.SetAllHubPower(new EnginePower(100));
        TestHelpers.StepMill(mill, steps: 120 * 60); // a full simulated minute — comfortably at cruise.
        return mill;
    }

    /// <summary>Advances a mill until it is at rest, returning the number of steps taken.</summary>
    private static int StepsToRest(GreatMill mill)
    {
        var steps = 0;
        const int cap = 120 * 60; // a safety net so a regression can't loop forever.
        while (!mill.IsAtRest && steps < cap)
        {
            mill.AdvancePhysics(TestHelpers.Dt.TotalSeconds);
            steps++;
        }

        return steps;
    }

    /// <summary>An empty ride driven into the running <see cref="RideState.Started"/> state.</summary>
    private static Ride RunningRide()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        return ride;
    }
}
