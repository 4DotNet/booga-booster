using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A single person waiting for a ride (ADR-0003). Identity is the unique
/// <see cref="Number"/>; a person also carries a <see cref="Name"/> and a
/// <see cref="WeightInKilograms"/> that the booster's downstream weight
/// calculations depend on, plus their mood on arrival: a starting
/// <see cref="Happiness"/>, the <see cref="PreferredG"/> they consider fun and a
/// <see cref="Nausea"/>. All values are validated on construction and never
/// change afterwards — the happiness a guest reports while waiting is derived from
/// the starting value by <see cref="QueueWaitDecay"/>, not stored. Instances are
/// produced by <see cref="Filling.PersonGenerator"/>, which supplies the unique
/// number, a generated name, a realistically distributed weight and the mood.
/// </summary>
public sealed class Person : DomainModel
{
    /// <summary>The lightest weight a person may have, in whole kilograms.</summary>
    public const int MinWeightInKilograms = 30;

    /// <summary>The heaviest weight a person may have, in whole kilograms.</summary>
    public const int MaxWeightInKilograms = 150;

    /// <summary>The lowest a mood value (happiness or nausea) may be.</summary>
    public const double MinMood = 0d;

    /// <summary>The highest a mood value (happiness or nausea) may be.</summary>
    public const double MaxMood = 100d;

    /// <summary>
    /// The highest preferred G a guest may have, in g: the ride's safe G-force
    /// limit (<c>RideParameters.MaxGForce</c> in the DigitalTwin module). Mirrored
    /// here because the Queue module may not reference the DigitalTwin module; the
    /// two must be kept equal.
    /// </summary>
    public const double MaxPreferredG = 4.5d;

    /// <summary>The lowest preferred G a guest may have, in g: half the ride's safe limit.</summary>
    public const double MinPreferredG = MaxPreferredG / 2d;

    public Person(long number, string name, int weightInKilograms, double happiness, double preferredG, double nausea)
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

        if (!double.IsFinite(happiness) || happiness is < MinMood or > MaxMood)
        {
            throw new DomainValidationException($"Person happiness must be between {MinMood} and {MaxMood}.");
        }

        if (!double.IsFinite(preferredG) || preferredG is < MinPreferredG or > MaxPreferredG)
        {
            throw new DomainValidationException(
                $"Person preferred G must be between {MinPreferredG} g and {MaxPreferredG} g.");
        }

        if (!double.IsFinite(nausea) || nausea is < MinMood or > MaxMood)
        {
            throw new DomainValidationException($"Person nausea must be between {MinMood} and {MaxMood}.");
        }

        Number = number;
        Name = name;
        WeightInKilograms = weightInKilograms;
        Happiness = happiness;
        PreferredG = preferredG;
        Nausea = nausea;
    }

    /// <summary>A process-unique, ever-increasing number identifying this person.</summary>
    public long Number { get; private set; }

    /// <summary>The person's full name.</summary>
    public string Name { get; private set; }

    /// <summary>The person's weight in whole kilograms, within [30, 150].</summary>
    public int WeightInKilograms { get; private set; }

    /// <summary>
    /// The person's happiness on joining the queue, within [0, 100]. What they report
    /// while waiting is this value less the <see cref="QueueWaitDecay"/>.
    /// </summary>
    public double Happiness { get; private set; }

    /// <summary>
    /// The felt G-force (in g) this person considers fun, within
    /// [<see cref="MinPreferredG"/>, <see cref="MaxPreferredG"/>].
    /// </summary>
    public double PreferredG { get; private set; }

    /// <summary>The person's nausea, within [0, 100]. Zero for a freshly arrived guest.</summary>
    public double Nausea { get; private set; }
}
