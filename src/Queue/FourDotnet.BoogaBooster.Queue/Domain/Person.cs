using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A single person waiting for a ride (ADR-0003). Identity is the unique
/// <see cref="Number"/>; a person also carries a <see cref="Name"/>, a
/// <see cref="WeightInKilograms"/> that the booster's downstream weight
/// calculations depend on, and a mood: <see cref="ArrivalHappiness"/>,
/// <see cref="PreferredRideIntensity"/> and <see cref="Nausea"/>, each on a 0–100
/// scale. All values are validated on construction and never change afterwards.
/// Instances are produced by <see cref="Filling.PersonGenerator"/>, which supplies
/// the unique number, a generated name, a realistically distributed weight and a
/// randomised initial mood.
/// </summary>
/// <remarks>
/// Happiness erodes while the person waits, but it is never stored decayed: it is a
/// pure function of the wait (<see cref="HappinessAfterWaiting"/>, derived in
/// <c>docs/06-rider-experience.md</c> §6.2). A persistent queue must therefore store
/// the arrival happiness and the time the group joined, not the decayed value.
/// </remarks>
public sealed class Person : DomainModel
{
    /// <summary>The lightest weight a person may have, in whole kilograms.</summary>
    public const int MinWeightInKilograms = 30;

    /// <summary>The heaviest weight a person may have, in whole kilograms.</summary>
    public const int MaxWeightInKilograms = 150;

    /// <summary>The lowest value of any mood score (very sad, gentlest, not nauseous).</summary>
    public const double MinMoodScore = 0d;

    /// <summary>The highest value of any mood score (extremely happy, most intense, extremely nauseous).</summary>
    public const double MaxMoodScore = 100d;

    /// <summary>How long a person waits before their happiness starts to decay (docs/06 §6.2, <c>T_patience</c>).</summary>
    public static readonly TimeSpan PatienceThreshold = TimeSpan.FromMinutes(5);

    /// <summary>The wait, beyond <see cref="PatienceThreshold"/>, that halves happiness (docs/06 §6.2, <c>T½</c>).</summary>
    public static readonly TimeSpan HappinessHalfLife = TimeSpan.FromMinutes(10);

    public Person(
        long number,
        string name,
        int weightInKilograms,
        double arrivalHappiness,
        double preferredRideIntensity,
        double nausea)
        : base(isNew: true)
    {
        if (number < 1)
        {
            throw new DomainValidationException("Person number must be a positive value.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Person name is required.");
        }

        if (weightInKilograms is < MinWeightInKilograms or > MaxWeightInKilograms)
        {
            throw new DomainValidationException(
                $"Person weight must be between {MinWeightInKilograms} and {MaxWeightInKilograms} kilograms.");
        }

        EnsureMoodScore(arrivalHappiness, "Happiness");
        EnsureMoodScore(preferredRideIntensity, "Preferred ride intensity");
        EnsureMoodScore(nausea, "Nausea");

        Number = number;
        Name = name;
        WeightInKilograms = weightInKilograms;
        ArrivalHappiness = arrivalHappiness;
        PreferredRideIntensity = preferredRideIntensity;
        Nausea = nausea;
    }

    /// <summary>A process-unique, ever-increasing number identifying this person.</summary>
    public long Number { get; private set; }

    /// <summary>The person's full name.</summary>
    public string Name { get; private set; }

    /// <summary>The person's weight in whole kilograms, within [30, 150].</summary>
    public int WeightInKilograms { get; private set; }

    /// <summary>How happy the person was when they joined the queue, within [0, 100].</summary>
    public double ArrivalHappiness { get; private set; }

    /// <summary>How intense the person likes their ride, within [0, 100].</summary>
    public double PreferredRideIntensity { get; private set; }

    /// <summary>How nauseous the person is, within [0, 100].</summary>
    public double Nausea { get; private set; }

    /// <summary>
    /// The person's happiness after waiting <paramref name="wait"/> in the queue: the
    /// arrival happiness for the first <see cref="PatienceThreshold"/>, then halving for
    /// every further <see cref="HappinessHalfLife"/>. A negative wait (a clock that
    /// moved backwards) is treated as no wait.
    /// </summary>
    public double HappinessAfterWaiting(TimeSpan wait)
    {
        var overdue = wait - PatienceThreshold;
        if (overdue <= TimeSpan.Zero)
        {
            return ArrivalHappiness;
        }

        return ArrivalHappiness * Math.Pow(2d, -overdue / HappinessHalfLife);
    }

    private static void EnsureMoodScore(double score, string label)
    {
        if (double.IsNaN(score) || score is < MinMoodScore or > MaxMoodScore)
        {
            throw new DomainValidationException(
                $"{label} must be between {MinMoodScore} and {MaxMoodScore}.");
        }
    }
}
