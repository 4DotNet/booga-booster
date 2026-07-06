using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>Domain helpers for <see cref="MotorDirection"/>.</summary>
public static class MotorDirectionExtensions
{
    /// <summary>
    /// The direction's sign applied to the drive torque: <c>+1</c> for
    /// <see cref="MotorDirection.Forward"/>, <c>-1</c> for
    /// <see cref="MotorDirection.Reverse"/>.
    /// </summary>
    public static double Sign(this MotorDirection direction) =>
        direction == MotorDirection.Reverse ? -1d : 1d;
}
