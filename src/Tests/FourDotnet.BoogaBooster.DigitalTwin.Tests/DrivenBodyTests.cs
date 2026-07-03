using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class DrivenBodyTests
{
    [Fact]
    public void The_motor_produces_finite_torque_at_standstill_and_starts_spinning()
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100));

        mill.AdvancePhysics(TestHelpers.Dt.TotalSeconds);

        Assert.True(double.IsFinite(mill.AngularVelocity));
        Assert.True(mill.AngularVelocity > 0d, "The mill should start accelerating from rest.");
    }

    [Fact]
    public void Power_drives_the_mill_up_to_but_not_past_the_over_speed_cap()
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100));

        TestHelpers.StepMill(mill, steps: 120 * 60); // one simulated minute

        Assert.True(mill.AngularVelocity > 1.0d, "The mill should have spun up.");
        Assert.True(
            mill.AngularVelocity <= RideParameters.MillMaxAngularVelocity + 1e-6d,
            "The over-speed cap must never be exceeded.");
    }

    [Fact]
    public void Kinetic_energy_never_increases_while_coasting_down()
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100));
        TestHelpers.StepMill(mill, steps: 120 * 20);

        // Cut power: the mill may only lose energy from here on.
        mill.SetPower(EnginePower.Off);

        var previous = mill.KineticEnergy();
        for (var i = 0; i < 120 * 20; i++)
        {
            mill.AdvancePhysics(TestHelpers.Dt.TotalSeconds);
            var energy = mill.KineticEnergy();
            Assert.True(energy <= previous + 1e-6d, $"Energy rose while coasting: {previous} → {energy}.");
            previous = energy;
        }
    }

    [Fact]
    public void A_heavier_mill_takes_longer_to_reach_cruise_at_the_same_power()
    {
        var empty = new GreatMill();
        var loaded = new GreatMill();
        foreach (var hub in loaded.Hubs)
        {
            TestHelpers.FillHub(hub, RideParameters.MaxPassengerKg);
        }

        var stepsEmpty = StepsToReach(empty, targetOmega: 1.0d);
        var stepsLoaded = StepsToReach(loaded, targetOmega: 1.0d);

        Assert.True(
            stepsLoaded > stepsEmpty,
            $"A loaded mill ({stepsLoaded} steps) should be slower than empty ({stepsEmpty} steps).");
    }

    [Fact]
    public void Two_hubs_on_equal_power_spin_at_different_speeds_when_their_loads_differ()
    {
        var mill = new GreatMill();
        TestHelpers.FillHub(mill.GetHub(0), RideParameters.MaxPassengerKg); // heavy
        // hub 1 stays empty.
        mill.SetAllHubPower(new EnginePower(100));

        TestHelpers.StepMill(mill, steps: 120 * 5);

        Assert.True(
            mill.GetHub(0).AngularVelocity < mill.GetHub(1).AngularVelocity,
            "The heavier hub should spin slower on the same power.");
    }

    private static int StepsToReach(GreatMill mill, double targetOmega)
    {
        mill.SetPower(new EnginePower(100));
        var steps = 0;
        while (mill.AngularVelocity < targetOmega && steps < 120 * 600)
        {
            mill.AdvancePhysics(TestHelpers.Dt.TotalSeconds);
            steps++;
        }

        return steps;
    }
}
