using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// How a rider is getting on (ADR-0003 value object, design D1): their
/// <see cref="Happiness"/>, <see cref="PreferredRideIntensity"/> and
/// <see cref="Nausea"/>, each on the
/// <c>[<see cref="RideParameters.ExperienceScaleMin"/>, <see cref="RideParameters.ExperienceScaleMax"/>]</c>
/// scale of <c>docs/06-passenger-experience.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// Construction validates every value and throws <see cref="DomainValidationException"/>
/// when one is out of range. The intent-revealing mutators (<see cref="RaiseHappiness"/>,
/// <see cref="AddNausea"/>) never throw on range: they are driven by continuous
/// dynamics, so they clamp to the scale and return a new value.
/// </para>
/// <para>
/// A <c>readonly record struct</c> rather than a class because it is replaced for every
/// seated rider on every 120 Hz physics tick: as a value type each update is a register
/// copy, not a heap allocation. Its <c>default</c> (all zeros) is a valid experience, so
/// the struct cannot exist in an invalid state either.
/// </para>
/// </remarks>
public readonly record struct RiderExperience
{
    public RiderExperience(double happiness, double preferredRideIntensity, double nausea)
    {
        EnsureOnScale(happiness, "happiness");
        EnsureOnScale(preferredRideIntensity, "preferred ride intensity");
        EnsureOnScale(nausea, "nausea");

        Happiness = happiness;
        PreferredRideIntensity = preferredRideIntensity;
        Nausea = nausea;
    }

    /// <summary>
    /// The experience of a rider boarded without a queue history: the midpoints of the
    /// queue's arrival bands and no nausea (docs/06 §6.1).
    /// </summary>
    public static RiderExperience Default { get; } = new(
        RideParameters.DefaultRiderHappiness,
        RideParameters.DefaultRiderPreferredIntensity,
        RideParameters.ExperienceScaleMin);

    /// <summary>0 is very sad, 100 is extremely happy.</summary>
    public double Happiness { get; }

    /// <summary>0 is very tame, 100 is the maximum allowed G-force.</summary>
    public double PreferredRideIntensity { get; }

    /// <summary>0 is fine, 100 is extremely nauseous.</summary>
    public double Nausea { get; }

    /// <summary>Returns this experience with <paramref name="points"/> more happiness, held at the top of the scale.</summary>
    /// <exception cref="DomainValidationException"><paramref name="points"/> is negative or not a number.</exception>
    public RiderExperience RaiseHappiness(double points)
    {
        EnsureIncrease(points, "happiness");
        return new RiderExperience(Clamp(Happiness + points), PreferredRideIntensity, Nausea);
    }

    /// <summary>Returns this experience with <paramref name="points"/> more nausea, held at the top of the scale.</summary>
    /// <exception cref="DomainValidationException"><paramref name="points"/> is negative or not a number.</exception>
    public RiderExperience AddNausea(double points)
    {
        EnsureIncrease(points, "nausea");
        return new RiderExperience(Happiness, PreferredRideIntensity, Clamp(Nausea + points));
    }

    /// <summary>
    /// Returns this experience after riding for <paramref name="dt"/> seconds at
    /// <paramref name="intensity"/> (docs/06 §6.4–6.5). With
    /// <c>Δ = intensity − PreferredRideIntensity</c>:
    /// <list type="bullet">
    /// <item>within <see cref="RideParameters.HappinessMatchBand"/> of the preference,
    /// happiness rises at <see cref="RideParameters.HappinessGainRate"/> scaled by how
    /// close the match is (<c>1 − |Δ| / band</c>);</item>
    /// <item>at least <see cref="RideParameters.NauseaIntensityExcess"/> above the
    /// preference, nausea grows exponentially:
    /// <c>N += NauseaGrowthRate · (N + NauseaSeed) · dt</c>.</item>
    /// </list>
    /// Anything else leaves the experience unchanged. Pure arithmetic on the fixed step:
    /// no clock, no randomness, no allocation.
    /// </summary>
    /// <exception cref="DomainValidationException"><paramref name="dt"/> is not positive.</exception>
    public RiderExperience AfterRiding(double intensity, double dt)
    {
        if (!(dt > 0d))
        {
            throw new DomainValidationException("The riding step must be positive.");
        }

        var excess = intensity - PreferredRideIntensity;
        var mismatch = Math.Abs(excess);

        var happiness = Happiness;
        if (mismatch <= RideParameters.HappinessMatchBand)
        {
            var closeness = 1d - (mismatch / RideParameters.HappinessMatchBand);
            happiness = Clamp(happiness + (RideParameters.HappinessGainRate * closeness * dt));
        }

        var nausea = Nausea;
        if (excess >= RideParameters.NauseaIntensityExcess)
        {
            nausea = Clamp(nausea + (RideParameters.NauseaGrowthRate * (nausea + RideParameters.NauseaSeed) * dt));
        }

        return new RiderExperience(happiness, PreferredRideIntensity, nausea);
    }

    private static double Clamp(double value) =>
        Math.Clamp(value, RideParameters.ExperienceScaleMin, RideParameters.ExperienceScaleMax);

    private static void EnsureOnScale(double value, string attribute)
    {
        // The negated range check also rejects NaN, which compares false to everything.
        if (!(value >= RideParameters.ExperienceScaleMin && value <= RideParameters.ExperienceScaleMax))
        {
            throw new DomainValidationException(
                $"Rider {attribute} must be between {RideParameters.ExperienceScaleMin} and {RideParameters.ExperienceScaleMax}.");
        }
    }

    private static void EnsureIncrease(double points, string attribute)
    {
        if (!(points >= 0d))
        {
            throw new DomainValidationException($"A rise in {attribute} cannot be negative or not a number.");
        }
    }
}
