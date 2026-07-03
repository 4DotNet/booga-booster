using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Source of the twin's bounded randomness: a boarding passenger's weight and how
/// long after sitting down they pull their restraint closed. Abstracted so it can
/// be seeded (or faked) for deterministic, reproducible runs.
/// </summary>
public interface IRideEventSampler
{
    /// <summary>A random passenger weight within the allowed range.</summary>
    PassengerWeight NextPassengerWeight();

    /// <summary>A random natural restraint-close delay (10–30 s by default).</summary>
    TimeSpan NextRestraintCloseDelay();
}
