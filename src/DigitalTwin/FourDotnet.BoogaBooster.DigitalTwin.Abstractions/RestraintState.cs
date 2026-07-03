namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The state of a seat's safety restraint, reported by its lock sensor. A ride may
/// only start when every occupied seat's restraint is <see cref="Secured"/>.
/// </summary>
public enum RestraintState
{
    /// <summary>The bar is up; a passenger can board or leave.</summary>
    Open,

    /// <summary>The bar is pulled down but the lock has not yet engaged.</summary>
    Closed,

    /// <summary>The bar is down and the lock has engaged — safe to dispatch.</summary>
    Secured
}
