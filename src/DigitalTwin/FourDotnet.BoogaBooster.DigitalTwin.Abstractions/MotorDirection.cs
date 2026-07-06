namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The commanded rotation direction of a motor group. <see cref="Forward"/> drives
/// a body in the positive direction, <see cref="Reverse"/> in the negative one. The
/// operator commands direction independently of power; the resulting motion emerges
/// from the drive torque's sign, the friction, and the load.
/// </summary>
public enum MotorDirection
{
    /// <summary>Drive the body in the positive (default) direction.</summary>
    Forward = 0,

    /// <summary>Drive the body in the negative direction.</summary>
    Reverse = 1
}
