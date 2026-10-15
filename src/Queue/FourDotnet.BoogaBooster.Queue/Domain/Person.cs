using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A single person waiting for a ride (ADR-0003). Identity is the unique
/// <see cref="Number"/>; a person also carries a <see cref="Name"/>, a
/// <see cref="WeightInKilograms"/> that the booster's downstream weight
/// calculations depend on, and three experience attributes on a 0–100 scale:
/// <see cref="Happiness"/>, <see cref="PreferredRideIntensity"/> and
/// <see cref="Nausea"/>. All values are validated on construction and never
/// change afterwards — happiness lost to waiting is derived from the enqueue time
/// (<see cref="HappinessAfterWaiting"/>), not written back. Instances are produced
/// by <see cref="Filling.PersonGenerator"/>, which supplies the unique number, a
/// generated name, a realistically distributed weight and the starting experience.
/// </summary>
public sealed class Person : DomainModel
{
    /// <summary>The lightest weight a person may have, in whole kilograms.</summary>
    public const int MinWeightInKilograms = 30;

    /// <summary>The heaviest weight a person may have, in whole kilograms.</summary>
    public const int MaxWeightInKilograms = 150;

    /// <summary>The lowest value of an experience attribute (very sad, very tame, not nauseous).</summary>
    public const double MinExperienceRating = 0d;

    /// <summary>The highest value of an experience attribute (extremely happy, maximum G, extremely nauseous).</summary>
    public const double MaxExperienceRating = 100d;

    public Person(
        long number,
        string name,
        int weightInKilograms,
        double happiness,
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

        EnsureRating(happiness, "happiness");
        EnsureRating(preferredRideIntensity, "preferred ride intensity");
        EnsureRating(nausea, "nausea");

        Number = number;
        Name = name;
        WeightInKilograms = weightInKilograms;
        Happiness = happiness;
        PreferredRideIntensity = preferredRideIntensity;
        Nausea = nausea;
    }

    /// <summary>A process-unique, ever-increasing number identifying this person.</summary>
    public long Number { get; private set; }

    /// <summary>The person's full name.</summary>
    public string Name { get; private set; }

    /// <summary>The person's weight in whole kilograms, within [30, 150].</summary>
    public int WeightInKilograms { get; private set; }

    /// <summary>
    /// How happy the person was on arrival, within [0, 100]: 0 is very sad, 100 is
    /// extremely happy.
    /// </summary>
    public double Happiness { get; private set; }

    /// <summary>
    /// The ride intensity the person enjoys most, within [0, 100]: 0 is very tame,
    /// 100 is the maximum allowed G-force.
    /// </summary>
    public double PreferredRideIntensity { get; private set; }

    /// <summary>How nauseous the person is, within [0, 100]: 0 is fine, 100 is extremely nauseous.</summary>
    public double Nausea { get; private set; }

    /// <summary>
    /// The person's happiness after waiting <paramref name="waited"/> in the queue —
    /// their arrival <see cref="Happiness"/> worn down by <paramref name="decay"/>.
    /// The person itself is not changed: queue happiness is always derived from the
    /// enqueue time, so it is exact at any instant it is read.
    /// </summary>
    public double HappinessAfterWaiting(TimeSpan waited, HappinessDecay decay)
    {
        ArgumentNullException.ThrowIfNull(decay);

        return decay.Apply(Happiness, waited);
    }

    private static void EnsureRating(double value, string attribute)
    {
        // The negated range check also rejects NaN, which compares false to everything.
        if (!(value >= MinExperienceRating && value <= MaxExperienceRating))
        {
            throw new DomainValidationException(
                $"Person {attribute} must be between {MinExperienceRating} and {MaxExperienceRating}.");
        }
    }
}
