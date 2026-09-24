using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// The mood a rider brings onto the ride, validated on construction (ADR-0003) like
/// <see cref="PassengerWeight"/>: how intense a ride they like, how happy they are
/// and how nauseous they feel. It is the boarding snapshot only — once seated, the
/// <see cref="Passenger"/> owns the evolving happiness and nausea
/// (<c>docs/06-rider-experience.md</c>).
/// </summary>
public sealed record RiderProfile
{
    /// <summary>The tamest ride anyone prefers; a preference of zero would match a standstill.</summary>
    public const double MinPreferredIntensity = 0.1d;

    /// <summary>The most intense ride anyone prefers — the maximum allowed G.</summary>
    public const double MaxPreferredIntensity = 1d;

    /// <summary>The floor of the happiness and nausea scales.</summary>
    public const double MinMood = 0d;

    /// <summary>The ceiling of the happiness and nausea scales.</summary>
    public const double MaxMood = 1d;

    public RiderProfile(double preferredIntensity, double happiness, double nausea)
    {
        if (!double.IsFinite(preferredIntensity) || !double.IsFinite(happiness) || !double.IsFinite(nausea))
        {
            throw new DomainValidationException("A rider profile's values must be finite numbers.");
        }

        if (preferredIntensity < MinPreferredIntensity || preferredIntensity > MaxPreferredIntensity)
        {
            throw new DomainValidationException(
                $"Preferred intensity must be between {MinPreferredIntensity} and {MaxPreferredIntensity}.");
        }

        if (happiness < MinMood || happiness > MaxMood)
        {
            throw new DomainValidationException($"Happiness must be between {MinMood} and {MaxMood}.");
        }

        if (nausea < MinMood || nausea > MaxMood)
        {
            throw new DomainValidationException($"Nausea must be between {MinMood} and {MaxMood}.");
        }

        PreferredIntensity = preferredIntensity;
        Happiness = happiness;
        Nausea = nausea;
    }

    /// <summary>How intense a ride the rider likes, on the gondola's <c>[0, 1]</c> intensity scale.</summary>
    public double PreferredIntensity { get; }

    /// <summary>How happy the rider is, in <c>[0, 1]</c> (1 is delighted).</summary>
    public double Happiness { get; }

    /// <summary>How nauseous the rider is, in <c>[0, 1]</c> (0 is fine).</summary>
    public double Nausea { get; }

    /// <summary>
    /// The profile of a rider created without one: a middle-of-the-road preference, the
    /// centre of the arrival happiness range and no nausea
    /// (<see cref="RideParameters.DefaultPreferredIntensity"/>, <see cref="RideParameters.DefaultHappiness"/>).
    /// </summary>
    public static RiderProfile Neutral { get; } =
        new(RideParameters.DefaultPreferredIntensity, RideParameters.DefaultHappiness, MinMood);
}
