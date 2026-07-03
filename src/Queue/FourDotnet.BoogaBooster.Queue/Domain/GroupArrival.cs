using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A validated value object describing a group of people arriving at a ride
/// (ADR-0003). Sets multiple values at once (group identity plus its members), so
/// it is fully validated on construction and can never exist empty or malformed.
/// Groups of real people are produced by <see cref="Filling.PersonGenerator"/>.
/// </summary>
public sealed class GroupArrival
{
    public GroupArrival(Guid groupId, IReadOnlyList<Person> members)
    {
        if (groupId == Guid.Empty)
        {
            throw new DomainValidationException("Group id is required.");
        }

        if (members is null || members.Count == 0)
        {
            throw new DomainValidationException("A group arrival must contain at least one person.");
        }

        if (members.Any(m => m is null))
        {
            throw new DomainValidationException("A group arrival cannot contain a null person.");
        }

        GroupId = groupId;
        Members = members.ToArray();
    }

    public Guid GroupId { get; }

    public IReadOnlyList<Person> Members { get; }

    public int Size => Members.Count;

    /// <summary>The combined weight of everyone in the group, in kilograms.</summary>
    public int TotalWeightInKilograms => Members.Sum(m => m.WeightInKilograms);
}
