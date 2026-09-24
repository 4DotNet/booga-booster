using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A guest's rider profile, validated on construction (ADR-0003): how intense a ride
/// they like, how happy they are and how nauseous they feel. The three values always
/// travel together, so they form one value object rather than three loose doubles on
/// <see cref="Person"/>. Instances are immutable; a wait in the line never rewrites
/// the stored <see cref="Happiness"/> — see <see cref="GrumpinessPolicy"/> for how the
/// <em>current</em> happiness is derived from it.
/// </summary>
public sealed record RiderProfile
{
    /// <summary>The tamest ride preference a rider may have.</summary>
    public const double MinPreferredIntensity = 0.1;

    /// <summary>The most intense ride preference a rider may have — the ride at its G limit.</summary>
    public const double MaxPreferredIntensity = 1.0;

    /// <summary>Extremely unhappy.</summary>
    public const double MinHappiness = 0.0;

    /// <summary>Very happy.</summary>
    public const double MaxHappiness = 1.0;

    /// <summary>Not nauseous at all.</summary>
    public const double MinNausea = 0.0;

    /// <summary>Maximally nauseous.</summary>
    public const double MaxNausea = 1.0;

    public RiderProfile(double preferredIntensity, double happiness, double nausea)
    {
        if (!double.IsFinite(preferredIntensity)
            || preferredIntensity is < MinPreferredIntensity or > MaxPreferredIntensity)
        {
            throw new DomainValidationException(
                $"Preferred intensity must be a finite number between {MinPreferredIntensity} and {MaxPreferredIntensity}.");
        }

        if (!double.IsFinite(happiness) || happiness is < MinHappiness or > MaxHappiness)
        {
            throw new DomainValidationException(
                $"Happiness must be a finite number between {MinHappiness} and {MaxHappiness}.");
        }

        if (!double.IsFinite(nausea) || nausea is < MinNausea or > MaxNausea)
        {
            throw new DomainValidationException(
                $"Nausea must be a finite number between {MinNausea} and {MaxNausea}.");
        }

        PreferredIntensity = preferredIntensity;
        Happiness = happiness;
        Nausea = nausea;
    }

    /// <summary>
    /// How intense a ride this rider likes, in
    /// <c>[<see cref="MinPreferredIntensity"/>, <see cref="MaxPreferredIntensity"/>]</c>,
    /// on the same scale as a gondola's felt-G intensity.
    /// </summary>
    public double PreferredIntensity { get; }

    /// <summary>
    /// The rider's happiness in <c>[<see cref="MinHappiness"/>, <see cref="MaxHappiness"/>]</c>
    /// at the moment the profile was drawn — the value they arrived with.
    /// </summary>
    public double Happiness { get; }

    /// <summary>The rider's nausea in <c>[<see cref="MinNausea"/>, <see cref="MaxNausea"/>]</c>.</summary>
    public double Nausea { get; }
}
