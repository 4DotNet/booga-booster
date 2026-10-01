using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A single rider the twin accepts as a load: a validated body weight plus the
/// guest's identity and mood, carried from the queue into the seat and out of the
/// exit. Where they sit and whether their restraint is secured is owned by the
/// <see cref="Seat"/> they board. Weight, identity and preference never change;
/// happiness and nausea move only through <see cref="GainHappiness"/> and
/// <see cref="GainNausea"/>, which clamp to [<see cref="RideParameters.MinMood"/>,
/// <see cref="RideParameters.MaxMood"/>] in one place.
/// </summary>
public sealed class Passenger : DomainModel
{
    private double _happiness;
    private double _nausea;

    public Passenger(PassengerSeed seed)
        : base(isNew: true)
    {
        ArgumentNullException.ThrowIfNull(seed);

        GuestNumber = seed.GuestNumber;
        Weight = seed.Weight;
        PreferredG = seed.PreferredG;
        _happiness = seed.Happiness;
        _nausea = seed.Nausea;
    }

    /// <summary>Creates an anonymous passenger (no queue identity, default mood) of the given weight.</summary>
    public Passenger(PassengerWeight weight)
        : this(PassengerSeed.Anonymous(weight))
    {
    }

    /// <summary>The guest's number from the queue, or <c>null</c> for a rider boarded by hand.</summary>
    public long? GuestNumber { get; }

    /// <summary>The passenger's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>The felt G-force (in g) the rider considers fun.</summary>
    public double PreferredG { get; }

    /// <summary>The rider's current happiness, in [0, 100].</summary>
    public double Happiness => _happiness;

    /// <summary>The rider's current nausea, in [0, 100].</summary>
    public double Nausea => _nausea;

    /// <summary>Creates an anonymous passenger of the given weight in kilograms.</summary>
    public static Passenger OfWeight(double kilograms) => new(new PassengerWeight(kilograms));

    /// <summary>Raises the rider's happiness by <paramref name="points"/>, saturating at the maximum.</summary>
    /// <exception cref="DomainValidationException"><paramref name="points"/> is negative or not finite.</exception>
    public void GainHappiness(double points)
    {
        EnsureValidGain(points);
        ApplyChange(ref _happiness, Clamp(_happiness + points));
    }

    /// <summary>Raises the rider's nausea by <paramref name="points"/>, saturating at the maximum.</summary>
    /// <exception cref="DomainValidationException"><paramref name="points"/> is negative or not finite.</exception>
    public void GainNausea(double points)
    {
        EnsureValidGain(points);
        ApplyChange(ref _nausea, Clamp(_nausea + points));
    }

    private static double Clamp(double value) =>
        Math.Clamp(value, RideParameters.MinMood, RideParameters.MaxMood);

    private static void EnsureValidGain(double points)
    {
        if (!double.IsFinite(points) || points < 0d)
        {
            throw new DomainValidationException("A mood gain must be a finite, non-negative number of points.");
        }
    }
}
