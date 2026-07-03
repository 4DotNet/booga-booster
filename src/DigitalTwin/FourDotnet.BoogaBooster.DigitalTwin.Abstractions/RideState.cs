namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The lifecycle state of the whole ride. Drives which operator commands are
/// allowed: for example the ride can only move from <see cref="Ready"/> to
/// <see cref="Running"/>, and only when every occupied restraint is secured and the
/// load is balanced.
/// </summary>
public enum RideState
{
    /// <summary>Stopped and empty; passengers may board.</summary>
    Idle,

    /// <summary>Passengers are boarding and securing their restraints.</summary>
    Boarding,

    /// <summary>All occupied restraints are secured and the load is balanced — safe to start.</summary>
    Ready,

    /// <summary>The ride is spinning.</summary>
    Running,

    /// <summary>The ride is ramping down to a stop.</summary>
    Stopping,

    /// <summary>A safety interlock tripped; requires an explicit operator reset.</summary>
    Faulted
}
