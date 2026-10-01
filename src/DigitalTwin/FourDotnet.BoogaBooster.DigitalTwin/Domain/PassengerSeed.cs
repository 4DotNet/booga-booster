using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// Everything a rider brings onto the ride, validated as a unit before any seat
/// accepts it (ADR-0003): who they are, what they weigh and how they feel. A
/// boarding group is a list of these, built from the queue's view of each guest.
/// </summary>
public sealed record PassengerSeed
{
    public PassengerSeed(long? guestNumber, PassengerWeight weight, double happiness, double preferredG, double nausea)
    {
        ArgumentNullException.ThrowIfNull(weight);

        if (guestNumber is < 1)
        {
            throw new DomainValidationException("A guest number must be a positive value.");
        }

        if (!double.IsFinite(happiness) || happiness is < RideParameters.MinMood or > RideParameters.MaxMood)
        {
            throw new DomainValidationException(
                $"Passenger happiness must be between {RideParameters.MinMood} and {RideParameters.MaxMood}.");
        }

        if (!double.IsFinite(preferredG) || preferredG is < RideParameters.MinPreferredG or > RideParameters.MaxPreferredG)
        {
            throw new DomainValidationException(
                $"Passenger preferred G must be between {RideParameters.MinPreferredG} g and {RideParameters.MaxPreferredG} g.");
        }

        if (!double.IsFinite(nausea) || nausea is < RideParameters.MinMood or > RideParameters.MaxMood)
        {
            throw new DomainValidationException(
                $"Passenger nausea must be between {RideParameters.MinMood} and {RideParameters.MaxMood}.");
        }

        GuestNumber = guestNumber;
        Weight = weight;
        Happiness = happiness;
        PreferredG = preferredG;
        Nausea = nausea;
    }

    /// <summary>The guest's number from the queue, or <c>null</c> for a rider boarded by hand.</summary>
    public long? GuestNumber { get; }

    /// <summary>The rider's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>The rider's happiness on boarding, in [0, 100].</summary>
    public double Happiness { get; }

    /// <summary>The felt G-force (in g) the rider considers fun.</summary>
    public double PreferredG { get; }

    /// <summary>The rider's nausea on boarding, in [0, 100].</summary>
    public double Nausea { get; }

    /// <summary>
    /// A rider of the given weight with no queue identity and the default mood — used
    /// when a passenger is boarded by hand rather than from the queue.
    /// </summary>
    public static PassengerSeed Anonymous(PassengerWeight weight) => new(
        guestNumber: null,
        weight,
        RideParameters.DefaultPassengerHappiness,
        RideParameters.DefaultPassengerPreferredG,
        RideParameters.MinMood);
}
