using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// The commanded rotation direction on the mill and hubs, and the reversing drive
/// dynamics that follow a direction change while spinning.
/// </summary>
public sealed class MotorDirectionTests
{
    [Fact]
    public void A_new_mill_and_its_hubs_start_forward()
    {
        var mill = new GreatMill();

        Assert.Equal(MotorDirection.Forward, mill.Direction);
        foreach (var hub in mill.Hubs)
        {
            Assert.Equal(MotorDirection.Forward, hub.Direction);
        }
    }

    [Fact]
    public void Setting_the_direction_to_a_new_value_changes_it_and_reports_a_change()
    {
        var mill = new GreatMill();

        var changed = mill.SetDirection(MotorDirection.Reverse);

        Assert.True(changed);
        Assert.Equal(MotorDirection.Reverse, mill.Direction);
    }

    [Fact]
    public void Setting_the_direction_to_the_current_value_is_a_no_op()
    {
        var mill = new GreatMill();

        var changed = mill.SetDirection(MotorDirection.Forward);

        Assert.False(changed);
        Assert.Equal(MotorDirection.Forward, mill.Direction);
    }

    [Fact]
    public void Setting_all_hub_directions_applies_to_every_hub()
    {
        var mill = new GreatMill();

        mill.SetAllHubDirection(MotorDirection.Reverse);

        foreach (var hub in mill.Hubs)
        {
            Assert.Equal(MotorDirection.Reverse, hub.Direction);
        }
    }

    [Fact]
    public void Direction_is_independent_of_power()
    {
        var mill = new GreatMill();

        mill.SetDirection(MotorDirection.Reverse);
        mill.SetPower(new EnginePower(75));

        Assert.Equal(MotorDirection.Reverse, mill.Direction);
        Assert.Equal(75d, mill.Power.Percent);

        mill.SetPower(EnginePower.Off);
        Assert.Equal(MotorDirection.Reverse, mill.Direction);
    }

    [Fact]
    public void A_reversed_motor_spins_a_body_up_negative_from_rest()
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100));
        mill.SetDirection(MotorDirection.Reverse);

        TestHelpers.StepMill(mill, steps: 120 * 5);

        Assert.True(mill.AngularVelocity < 0d, "A reversed mill should spin up in the negative direction.");
        Assert.True(RotationalDynamicsToRpm(mill.AngularVelocity) < 0d, "Reported speed should be signed negative.");
    }

    [Fact]
    public void Reversing_a_spinning_body_slows_it_through_rest_then_accelerates_it_the_other_way()
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100));
        TestHelpers.StepMill(mill, steps: 120 * 20);

        var forwardSpeed = mill.AngularVelocity;
        Assert.True(forwardSpeed > 0d, "The mill should be spinning forward before the reversal.");

        // Command reverse while power stays applied.
        mill.SetDirection(MotorDirection.Reverse);

        // First step: the drive now opposes the motion, so it must be slowing down.
        mill.AdvancePhysics(TestHelpers.Dt.TotalSeconds);
        Assert.True(
            mill.AngularVelocity < forwardSpeed,
            "The reversed drive should immediately slow a forward-spinning mill.");

        // Advance to the new reverse cruise and confirm it ends up turning the other way.
        var minAbs = Math.Abs(mill.AngularVelocity);
        for (var i = 0; i < 120 * 40; i++)
        {
            mill.AdvancePhysics(TestHelpers.Dt.TotalSeconds);
            minAbs = Math.Min(minAbs, Math.Abs(mill.AngularVelocity));
        }

        Assert.True(mill.AngularVelocity < 0d, "The mill should end up spinning in reverse.");
        // The turnaround passes (near) through zero rather than jumping discontinuously.
        Assert.True(minAbs < 0.05d, $"The reversal should pass close to a standstill (min |ω| was {minAbs}).");
    }

    [Fact]
    public void The_turnaround_never_jumps_sign_in_a_single_step()
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100));
        TestHelpers.StepMill(mill, steps: 120 * 20);

        mill.SetDirection(MotorDirection.Reverse);

        var previous = mill.AngularVelocity;
        for (var i = 0; i < 120 * 40; i++)
        {
            mill.AdvancePhysics(TestHelpers.Dt.TotalSeconds);
            var current = mill.AngularVelocity;

            // When the sign flips between two steps, the pre-flip speed must already
            // be small — the body eases through zero rather than teleporting across it.
            if (Math.Sign(current) != Math.Sign(previous) && previous != 0d && current != 0d)
            {
                Assert.True(
                    Math.Abs(previous) < 0.1d && Math.Abs(current) < 0.1d,
                    $"Sign flipped from {previous} to {current} without passing through zero.");
            }

            previous = current;
        }
    }

    // The domain's ToRpm is internal; a positive ω maps to a positive rpm and a
    // negative ω to a negative rpm, so this local mirror is all these tests need.
    private static double RotationalDynamicsToRpm(double omega) => omega * 60d / (2d * Math.PI);
}
