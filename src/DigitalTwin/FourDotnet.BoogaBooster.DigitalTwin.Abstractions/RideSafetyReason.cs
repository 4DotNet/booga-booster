namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// Why the ride is (not) safe to start. <see cref="None"/> means it is safe; every
/// other value is a blocking interlock reported to the operator.
/// </summary>
public enum RideSafetyReason
{
    /// <summary>No blocking condition — the ride is safe to start.</summary>
    None,

    /// <summary>At least one occupied seat's restraint is not yet secured.</summary>
    UnsecuredRestraint,

    /// <summary>The load is distributed too unevenly across the mill arms.</summary>
    UnbalancedLoad,

    /// <summary>The combined passenger weight exceeds the ride's maximum safe load.</summary>
    Overloaded,

    /// <summary>The ride is not in a state from which it can start.</summary>
    WrongState
}
