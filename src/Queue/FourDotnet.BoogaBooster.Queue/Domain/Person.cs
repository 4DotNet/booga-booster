using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A single person waiting for a ride (ADR-0003). Identity is the unique
/// <see cref="Number"/>; a person also carries a <see cref="Name"/>, a
/// <see cref="WeightInKilograms"/> that the booster's downstream weight
/// calculations depend on, and three experience ratings —
/// <see cref="Happiness"/>, <see cref="PreferredIntensity"/> and
/// <see cref="Nausea"/> (<c>docs/06-passenger-experience.md</c> §6.1). All values
/// are validated on construction and never change afterwards: the happiness held
/// here is the happiness on <em>arrival</em>, and the waited value is derived at
/// read time by <see cref="QueuePatience"/>. Instances are produced by
/// <see cref="Filling.PersonGenerator"/>.
/// </summary>
public sealed class Person : DomainModel
{
    /// <summary>The lightest weight a person may have, in whole kilograms.</summary>
    public const int MinWeightInKilograms = 30;

    /// <summary>The heaviest weight a person may have, in whole kilograms.</summary>
    public const int MaxWeightInKilograms = 150;

    /// <summary>The lowest value an experience rating may take.</summary>
    public const double MinRating = 0d;

    /// <summary>The highest value an experience rating may take.</summary>
    public const double MaxRating = 100d;

    public Person(
        long number,
        string name,
        int weightInKilograms,
        double happiness,
        double preferredIntensity,
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

        EnsureRating(happiness, nameof(Happiness));
        EnsureRating(preferredIntensity, nameof(PreferredIntensity));
        EnsureRating(nausea, nameof(Nausea));

        Number = number;
        Name = name;
        WeightInKilograms = weightInKilograms;
        Happiness = happiness;
        PreferredIntensity = preferredIntensity;
        Nausea = nausea;
    }

    /// <summary>A process-unique, ever-increasing number identifying this person.</summary>
    public long Number { get; private set; }

    /// <summary>The person's full name.</summary>
    public string Name { get; private set; }

    /// <summary>The person's weight in whole kilograms, within [30, 150].</summary>
    public int WeightInKilograms { get; private set; }

    /// <summary>
    /// The person's happiness on arrival at the queue, within [0, 100] (0 very sad,
    /// 100 extremely happy). Waiting erodes it; see <see cref="QueuePatience"/>.
    /// </summary>
    public double Happiness { get; private set; }

    /// <summary>The ride intensity the person prefers, within [0, 100] (0 tamest, 100 most intense).</summary>
    public double PreferredIntensity { get; private set; }

    /// <summary>The person's nausea, within [0, 100] (0 none, 100 maximum).</summary>
    public double Nausea { get; private set; }

    private static void EnsureRating(double value, string rating)
    {
        // The negated range check also rejects NaN, which compares false to everything.
        if (!(value is >= MinRating and <= MaxRating))
        {
            throw new DomainValidationException(
                $"Person {rating} must be between {MinRating} and {MaxRating}.");
        }
    }
}
