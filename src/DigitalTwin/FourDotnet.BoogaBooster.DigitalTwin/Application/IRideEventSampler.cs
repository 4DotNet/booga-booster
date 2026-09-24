using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Source of the twin's bounded randomness: a boarding passenger's weight and rider
/// profile, and how long after sitting down they pull their restraint closed.
/// Abstracted so it can be seeded (or faked) for deterministic, reproducible runs.
/// </summary>
public interface IRideEventSampler
{
    /// <summary>A random passenger weight within the allowed range.</summary>
    PassengerWeight NextPassengerWeight();

    /// <summary>
    /// A random rider profile for a passenger boarded without one — the same
    /// distribution as guests arriving in the queue: a uniform preferred intensity,
    /// a happiness in the boarding range and no nausea (docs/06 §6.4).
    /// </summary>
    RiderProfile NextRiderProfile();

    /// <summary>A random natural restraint-close delay (10–30 s by default).</summary>
    TimeSpan NextRestraintCloseDelay();

    /// <summary>
    /// A random selection of <paramref name="count"/> distinct positions in the range
    /// <c>[0, <paramref name="total"/>)</c> — used to choose which of the currently
    /// empty gondolas a boarding group takes, so a passenger grabs a random gondola
    /// and load spreads unpredictably across the mill. The returned positions are
    /// distinct and each is a valid index into a list of <paramref name="total"/> items.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="count"/> is negative or greater than <paramref name="total"/>.
    /// </exception>
    IReadOnlyList<int> NextGondolaSelection(int total, int count);
}
