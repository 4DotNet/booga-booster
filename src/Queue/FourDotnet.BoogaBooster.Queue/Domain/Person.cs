using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A single person waiting for a ride (ADR-0003). Identity is the unique
/// <see cref="Number"/>; a person also carries a <see cref="Name"/>, a
/// <see cref="WeightInKilograms"/> that the booster's downstream weight
/// calculations depend on, and a <see cref="Profile"/> describing the ride they
/// like and the mood they arrived in. All values are validated on construction and
/// never change afterwards — in particular a wait in the line never rewrites the
/// stored profile; see <see cref="QueuedGroup.CurrentHappiness"/>. Instances are
/// produced by <see cref="Filling.PersonGenerator"/>, which supplies the unique
/// number, a generated name, a realistically distributed weight and a randomly
/// drawn profile.
/// </summary>
public sealed class Person : DomainModel
{
    /// <summary>The lightest weight a person may have, in whole kilograms.</summary>
    public const int MinWeightInKilograms = 30;

    /// <summary>The heaviest weight a person may have, in whole kilograms.</summary>
    public const int MaxWeightInKilograms = 150;

    public Person(long number, string name, int weightInKilograms, RiderProfile profile)
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

        if (profile is null)
        {
            throw new DomainValidationException("A rider profile is required.");
        }

        Number = number;
        Name = name;
        WeightInKilograms = weightInKilograms;
        Profile = profile;
    }

    /// <summary>A process-unique, ever-increasing number identifying this person.</summary>
    public long Number { get; private set; }

    /// <summary>The person's full name.</summary>
    public string Name { get; private set; }

    /// <summary>The person's weight in whole kilograms, within [30, 150].</summary>
    public int WeightInKilograms { get; private set; }

    /// <summary>
    /// The ride the person likes and the mood they arrived in. The happiness stored
    /// here is the arrival value; the wait-adjusted value is derived, never written back.
    /// </summary>
    public RiderProfile Profile { get; private set; }
}
