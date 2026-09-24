using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A single rider the twin accepts as a load: a validated body weight plus the
/// mood they board with. The weight and preferred intensity never change; happiness
/// and nausea evolve every physics tick from the G-force the rider's gondola feels
/// (<c>docs/06-rider-experience.md</c>). Where they sit and whether their restraint
/// is secured is owned by the <see cref="Seat"/> they board.
/// </summary>
public sealed class Passenger : DomainModel
{
    public Passenger(PassengerWeight weight, RiderProfile profile)
        : base(isNew: true)
    {
        ArgumentNullException.ThrowIfNull(weight);
        ArgumentNullException.ThrowIfNull(profile);

        Weight = weight;
        PreferredIntensity = profile.PreferredIntensity;
        Happiness = profile.Happiness;
        Nausea = profile.Nausea;
    }

    /// <summary>The passenger's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>How intense a ride the passenger likes, on the gondola intensity scale <c>[0, 1]</c>. Fixed for the ride.</summary>
    public double PreferredIntensity { get; }

    /// <summary>How happy the passenger is right now, in <c>[0, 1]</c>.</summary>
    public double Happiness { get; private set; }

    /// <summary>How nauseous the passenger is right now, in <c>[0, 1]</c>.</summary>
    public double Nausea { get; private set; }

    /// <summary>Creates a passenger of the given weight in kilograms with the <see cref="RiderProfile.Neutral"/> profile.</summary>
    public static Passenger OfWeight(double kilograms) => new(new PassengerWeight(kilograms), RiderProfile.Neutral);

    /// <summary>
    /// Lets the passenger feel <paramref name="dt"/> seconds of a ride at
    /// <paramref name="intensity"/> (docs/06 §6.2). A ride within
    /// <see cref="RideParameters.IntensityMatchTolerance"/> of their preference makes
    /// them happier; a ride more intense than that makes them unhappier and nauseous;
    /// a tamer ride leaves them unchanged. Both moods stay clamped to <c>[0, 1]</c>.
    /// </summary>
    public void Experience(double intensity, double dt)
    {
        if (!double.IsFinite(intensity) || intensity < 0d || intensity > 1d)
        {
            throw new DomainValidationException("Ride intensity must be a finite number between 0 and 1.");
        }

        if (!double.IsFinite(dt) || dt < 0d)
        {
            throw new DomainValidationException("The experienced time step cannot be negative.");
        }

        var delta = intensity - PreferredIntensity;
        if (Math.Abs(delta) <= RideParameters.IntensityMatchTolerance)
        {
            Happiness = ClampMood(Happiness + (RideParameters.HappinessGainPerSecond * dt));
            MarkChanged();
        }
        else if (delta > 0d)
        {
            Happiness = ClampMood(Happiness - (RideParameters.HappinessLossPerSecond * dt));
            Nausea = ClampMood(Nausea + (RideParameters.NauseaGainPerSecond * dt));
            MarkChanged();
        }

        // delta < -tolerance: the ride is tamer than the rider likes. Boredom is not
        // modelled (design non-goal), so their mood is left as it is.
    }

    /// <summary>Adds <paramref name="amount"/> nausea, clamped to <c>[0, 1]</c> — the sustained-G penalty path (docs/06 §6.3).</summary>
    public void AddNausea(double amount)
    {
        if (!double.IsFinite(amount) || amount < 0d)
        {
            throw new DomainValidationException("Added nausea must be a finite, non-negative amount.");
        }

        Nausea = ClampMood(Nausea + amount);
        MarkChanged();
    }

    private static double ClampMood(double value) => Math.Clamp(value, RiderProfile.MinMood, RiderProfile.MaxMood);
}
