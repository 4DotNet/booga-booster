namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The lifecycle state of the whole ride, governed by a guarded state machine so
/// the ride can never follow a path that leaves passengers unsafe. Operator
/// commands drive most transitions (and are only offered when their guard passes);
/// the running states settle automatically when the ride reaches a physical
/// condition (at rest, or emptied).
/// </summary>
public enum RideState
{
    /// <summary>Stopped and empty; passengers may board and the ride does nothing.</summary>
    Idle,

    /// <summary>Groups are being loaded and passengers are securing their restraints.</summary>
    Loading,

    /// <summary>Everyone is loaded and every safety condition is met — safe to start.</summary>
    Safe,

    /// <summary>The ride is running with the safety constraints locked.</summary>
    Started,

    /// <summary>A controlled stop: power is cut, brakes applied, coasting to rest.</summary>
    Stopping,

    /// <summary>The ride is at rest, safety constraints released, and passengers are leaving.</summary>
    Offloading,

    /// <summary>An emergency stop is in progress: brakes applied immediately from an active state.</summary>
    EmergencyStop
}
