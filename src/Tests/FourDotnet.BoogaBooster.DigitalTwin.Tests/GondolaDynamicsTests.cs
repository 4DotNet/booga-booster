using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class GondolaDynamicsTests
{
    private const double MillOmega = 1.5d;

    // For hub 0 / gondola 0 at mill angle 0 the pivot lies on the +x axis, so the
    // outward field points along angle 0.
    private const double OutwardFieldAngle = 0d;

    [Fact]
    public void At_rest_no_horizontal_g_force_is_felt()
    {
        var gondola = new Gondola(0, 0);
        gondola.ReleaseBrake();

        gondola.AdvancePhysics(millAngle: 0d, millOmega: 0d, hubAngle: 0d, hubOmega: 0d, dt: TestHelpers.Dt.TotalSeconds);

        Assert.Equal(0d, gondola.LateralG, 6);
        Assert.Equal(0d, gondola.ForwardG, 6);
    }

    [Fact]
    public void An_engaged_brake_blocks_all_rotation()
    {
        var gondola = new Gondola(0, 0);
        gondola.EngageBrake();
        var angleBefore = gondola.Angle;

        for (var i = 0; i < 600; i++)
        {
            gondola.AdvancePhysics(0d, MillOmega, 0d, 0d, TestHelpers.Dt.TotalSeconds);
        }

        Assert.Equal(0d, gondola.AngularVelocity);
        Assert.Equal(angleBefore, gondola.Angle);
    }

    [Fact]
    public void A_symmetric_load_settles_with_its_back_pointing_radially_outward()
    {
        // The "unbluffable" test (docs §4.6): constant speed, symmetric centred load
        // ⇒ the gondola's back settles pointing straight out.
        var gondola = Settle(leftKg: 80d, rightKg: 80d, seconds: 30d);

        TestHelpers.AssertAngleClose(OutwardFieldAngle, gondola.BackWorldAngle(0d, 0d), tolerance: 0.02d);
        Assert.True(Math.Abs(gondola.AngularVelocity) < 0.02d, "The gondola should have settled to rest.");
    }

    [Fact]
    public void A_single_sided_load_settles_lopsided_but_still_backs_outward()
    {
        var symmetric = Settle(leftKg: 80d, rightKg: 80d, seconds: 30d);
        var leftOnly = Settle(leftKg: 80d, rightKg: 0d, seconds: 30d);

        // The centre of mass still ends up pointing outward in both cases...
        TestHelpers.AssertAngleClose(OutwardFieldAngle, leftOnly.BackWorldAngle(0d, 0d), tolerance: 0.02d);

        // ...but a one-sided load makes the gondola itself sit at a different, lopsided
        // yaw than a symmetric load.
        var difference = Math.Abs(Math.Atan2(
            Math.Sin(leftOnly.Angle - symmetric.Angle),
            Math.Cos(leftOnly.Angle - symmetric.Angle)));
        Assert.True(difference > 0.15d, $"Expected a lopsided settle but Δangle was only {difference} rad.");
    }

    [Fact]
    public void Load_changes_the_gondola_mass()
    {
        var empty = new Gondola(0, 0);
        var single = new Gondola(0, 0);
        single.Board(SeatPosition.Left, Passenger.OfWeight(100d), TimeSpan.Zero);
        var full = new Gondola(0, 0);
        full.Board(SeatPosition.Left, Passenger.OfWeight(100d), TimeSpan.Zero);
        full.Board(SeatPosition.Right, Passenger.OfWeight(100d), TimeSpan.Zero);

        Assert.Equal(RideParameters.EmptyGondolaKg, empty.TotalMass);
        Assert.Equal(RideParameters.EmptyGondolaKg + 100d, single.TotalMass);
        Assert.Equal(RideParameters.EmptyGondolaKg + 200d, full.TotalMass);
        Assert.Equal(200d, full.PassengerLoadKg);
    }

    [Fact]
    public void A_spinning_gondola_reports_g_forces_and_an_rpm()
    {
        var gondola = Settle(leftKg: 80d, rightKg: 80d, seconds: 5d);

        var telemetry = gondola.ToTelemetry();
        Assert.Equal(GondolaBrakeState.Released, telemetry.Brake);
        // Under a real centrifugal field the riders feel some horizontal load.
        Assert.True(Math.Abs(telemetry.ForwardG) + Math.Abs(telemetry.LateralG) > 0.1d);
    }

    private static Gondola Settle(double leftKg, double rightKg, double seconds)
    {
        var gondola = new Gondola(0, 0);
        if (leftKg > 0d)
        {
            gondola.Board(SeatPosition.Left, Passenger.OfWeight(leftKg), TimeSpan.Zero);
        }

        if (rightKg > 0d)
        {
            gondola.Board(SeatPosition.Right, Passenger.OfWeight(rightKg), TimeSpan.Zero);
        }

        gondola.ReleaseBrake();

        var steps = (int)(seconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            gondola.AdvancePhysics(0d, MillOmega, 0d, 0d, TestHelpers.Dt.TotalSeconds);
        }

        return gondola;
    }
}
