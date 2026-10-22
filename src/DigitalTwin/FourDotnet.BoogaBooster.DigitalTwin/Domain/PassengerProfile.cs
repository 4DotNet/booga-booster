using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// Everything the twin needs to know about a rider when they board: their body
/// weight and the mood they bring from the queue (ADR-0003: several values set at
/// once go through one validated value object). Each mood score lies within
/// [<see cref="RideParameters.MinMoodScore"/>, <see cref="RideParameters.MaxMoodScore"/>];
/// see <c>docs/06-rider-experience.md</c>.
/// </summary>
public sealed record PassengerProfile
{
    public PassengerProfile(
        PassengerWeight weight,
        double happiness,
        double preferredRideIntensity,
        double nausea)
    {
        ArgumentNullException.ThrowIfNull(weight);

        EnsureMoodScore(happiness, "Happiness");
        EnsureMoodScore(preferredRideIntensity, "Preferred ride intensity");
        EnsureMoodScore(nausea, "Nausea");

        Weight = weight;
        Happiness = happiness;
        PreferredRideIntensity = preferredRideIntensity;
        Nausea = nausea;
    }

    /// <summary>The rider's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>How happy the rider is as they board (0–100).</summary>
    public double Happiness { get; }

    /// <summary>How intense the rider likes their ride (0–100).</summary>
    public double PreferredRideIntensity { get; }

    /// <summary>How nauseous the rider is as they board (0–100).</summary>
    public double Nausea { get; }

    /// <summary>
    /// A rider of the given weight whose mood is unknown — for example one boarded by
    /// weight alone — with the default profile (happiness 75, preferred intensity 75,
    /// nausea 0).
    /// </summary>
    public static PassengerProfile WithDefaultMood(PassengerWeight weight) => new(
        weight,
        RideParameters.DefaultPassengerHappiness,
        RideParameters.DefaultPreferredRideIntensity,
        RideParameters.DefaultPassengerNausea);

    private static void EnsureMoodScore(double score, string label)
    {
        if (double.IsNaN(score) || score is < RideParameters.MinMoodScore or > RideParameters.MaxMoodScore)
        {
            throw new DomainValidationException(
                $"{label} must be between {RideParameters.MinMoodScore} and {RideParameters.MaxMoodScore}.");
        }
    }
}
