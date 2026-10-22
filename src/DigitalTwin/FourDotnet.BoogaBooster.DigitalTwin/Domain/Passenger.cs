using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A single rider the twin accepts as a load. A passenger has a validated body
/// weight and a mood — happiness, preferred ride intensity and nausea, each 0–100 —
/// that evolves while the ride is in motion (<c>docs/06-rider-experience.md</c>).
/// Where they sit and whether their restraint is secured is owned by the
/// <see cref="Seat"/> they board.
/// </summary>
public sealed class Passenger : DomainModel
{
    // k = ln 2 / T_N: the nausea growth rate that doubles N + N₀ every T_N (docs/06 §6.5).
    private static readonly double NauseaGrowthRate = Math.Log(2d) / RideParameters.NauseaDoublingSeconds;

    public Passenger(PassengerProfile profile)
        : base(isNew: true)
    {
        ArgumentNullException.ThrowIfNull(profile);

        Weight = profile.Weight;
        PreferredRideIntensity = profile.PreferredRideIntensity;
        Happiness = profile.Happiness;
        Nausea = profile.Nausea;
    }

    /// <summary>The passenger's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>How intense the passenger likes their ride (0–100). Fixed for the ride.</summary>
    public double PreferredRideIntensity { get; }

    /// <summary>How happy the passenger is (0–100).</summary>
    public double Happiness { get; private set; }

    /// <summary>How nauseous the passenger is (0–100).</summary>
    public double Nausea { get; private set; }

    /// <summary>Creates a passenger of the given weight in kilograms, with the default mood.</summary>
    public static Passenger OfWeight(double kilograms) =>
        new(PassengerProfile.WithDefaultMood(new PassengerWeight(kilograms)));

    /// <summary>
    /// Lets the passenger feel <paramref name="seconds"/> of a ride at
    /// <paramref name="intensity"/> (0–100). Happiness rises inside the comfort band
    /// around their preference (docs/06 §6.4); nausea grows exponentially once the
    /// intensity exceeds their preference by the nausea threshold (§6.5) and otherwise
    /// holds. Both stay within [0, 100].
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// <paramref name="intensity"/> is outside [0, 100], or <paramref name="seconds"/>
    /// is not a positive, finite number.
    /// </exception>
    public void ExperienceRide(double intensity, double seconds)
    {
        if (double.IsNaN(intensity) || intensity is < RideParameters.MinMoodScore or > RideParameters.MaxMoodScore)
        {
            throw new DomainValidationException(
                $"Ride intensity must be between {RideParameters.MinMoodScore} and {RideParameters.MaxMoodScore}.");
        }

        if (!double.IsFinite(seconds) || seconds <= 0d)
        {
            throw new DomainValidationException("The ride experience must last a positive, finite time.");
        }

        var overIntensity = intensity - PreferredRideIntensity;

        // Triangular comfort band: full gain at an exact match, none at ±B. The rate is
        // constant over the step, so the explicit update is exact.
        var comfort = Math.Max(0d, 1d - (Math.Abs(overIntensity) / RideParameters.ComfortHalfBand));
        var happiness = Math.Min(
            RideParameters.MaxMoodScore,
            Happiness + (RideParameters.HappinessGainPerSecond * comfort * seconds));

        // dN/dt = k(N + N₀), integrated exactly so the result does not depend on the step size.
        var nausea = Nausea;
        if (overIntensity >= RideParameters.NauseaIntensityThreshold)
        {
            nausea = ((Nausea + RideParameters.NauseaSeed) * Math.Exp(NauseaGrowthRate * seconds)) - RideParameters.NauseaSeed;
            nausea = Math.Min(RideParameters.MaxMoodScore, nausea);
        }

        var changed = happiness != Happiness || nausea != Nausea;
        Happiness = happiness;
        Nausea = nausea;
        MarkChanged(changed);
    }

    /// <summary>
    /// Applies one over-G shock: the passenger gains
    /// <see cref="RideParameters.OverGShockNausea"/> nausea, capped at 100 (docs/06 §6.6).
    /// </summary>
    public void SufferOverGShock()
    {
        var nausea = Math.Min(RideParameters.MaxMoodScore, Nausea + RideParameters.OverGShockNausea);
        var changed = nausea != Nausea;
        Nausea = nausea;
        MarkChanged(changed);
    }
}
