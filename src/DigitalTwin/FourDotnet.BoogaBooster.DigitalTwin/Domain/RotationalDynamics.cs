namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// The shared rotational-dynamics core (see <c>docs/02-rotational-dynamics.md</c>
/// and <c>docs/03-motor-power-and-torque.md</c>): the motor torque curve, the
/// friction/drag losses, and the symplectic (semi-implicit Euler) integrator used
/// by every rotating body. Pure functions — no state, no time, no randomness.
/// </summary>
internal static class RotationalDynamics
{
    private const double TwoPi = 2d * Math.PI;

    /// <summary>
    /// Torque delivered by a capped-power motor at angular speed
    /// <paramref name="omega"/>: flat at <paramref name="stallTorque"/> at low
    /// speed, then falling as <c>P/ω</c> in the power-limited region. The
    /// <see cref="RideParameters.OmegaEpsilon"/> floor prevents divide-by-zero at
    /// standstill.
    /// </summary>
    public static double MotorTorque(double throttleFraction, double omega, double stallTorque, double maxPowerWatts)
    {
        var commandedPower = throttleFraction * maxPowerWatts;
        var torque = commandedPower / Math.Max(Math.Abs(omega), RideParameters.OmegaEpsilon);
        return Math.Min(stallTorque, torque);
    }

    /// <summary>
    /// Advances one rotating body by <paramref name="dt"/> using semi-implicit
    /// Euler (velocity before position). Friction and drag always oppose motion and
    /// can never reverse it within a step, so a coasting body comes cleanly to rest.
    /// The result is clamped to <paramref name="maxAngularVelocity"/> (over-speed cap).
    /// </summary>
    public static RotationStep Integrate(
        double omega,
        double theta,
        double driveTorque,
        double coulombFriction,
        double viscousFriction,
        double aeroDrag,
        double inertia,
        double maxAngularVelocity,
        double dt)
    {
        var lossMagnitude = coulombFriction
            + (viscousFriction * Math.Abs(omega))
            + (aeroDrag * omega * omega);

        double netTorque;
        if (Math.Abs(omega) < 1e-9d)
        {
            // At rest, static friction holds the body until the drive exceeds it.
            if (driveTorque > coulombFriction)
            {
                netTorque = driveTorque - coulombFriction;
            }
            else if (driveTorque < -coulombFriction)
            {
                netTorque = driveTorque + coulombFriction;
            }
            else
            {
                return new RotationStep(0d, Wrap(theta));
            }
        }
        else
        {
            netTorque = driveTorque - (Math.Sign(omega) * lossMagnitude);
        }

        var alpha = netTorque / inertia;
        var newOmega = omega + (alpha * dt);

        // While coasting, losses must stop the body, not spin it up in reverse.
        if (driveTorque == 0d && omega != 0d && Math.Sign(newOmega) != Math.Sign(omega))
        {
            newOmega = 0d;
        }

        if (maxAngularVelocity > 0d)
        {
            newOmega = Math.Clamp(newOmega, -maxAngularVelocity, maxAngularVelocity);
        }

        return new RotationStep(newOmega, Wrap(theta + (newOmega * dt)));
    }

    /// <summary>Converts an angular velocity in rad/s to revolutions per minute.</summary>
    public static double ToRpm(double omega) => omega * 60d / TwoPi;

    /// <summary>Rotational kinetic energy <c>½·I·ω²</c> (joules).</summary>
    public static double KineticEnergy(double inertia, double omega) => 0.5d * inertia * omega * omega;

    /// <summary>Wraps an angle into <c>[0, 2π)</c>.</summary>
    public static double Wrap(double theta)
    {
        var wrapped = theta % TwoPi;
        return wrapped < 0d ? wrapped + TwoPi : wrapped;
    }
}

/// <summary>The result of one integration step: the new angular velocity and angle.</summary>
/// <param name="Omega">New angular velocity (rad/s).</param>
/// <param name="Theta">New angle, wrapped into <c>[0, 2π)</c> (rad).</param>
internal readonly record struct RotationStep(double Omega, double Theta);
