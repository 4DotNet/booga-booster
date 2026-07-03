using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A validated value object describing a group arriving at a ride (ADR-0003).
/// Sets multiple values at once (group identity plus its members), so it is fully
/// validated on construction and can never exist empty or malformed.
/// </summary>
public sealed class GroupArrival
{
    public GroupArrival(Guid groupId, IReadOnlyList<Guest> members)
    {
        if (groupId == Guid.Empty)
        {
            throw new DomainValidationException("Group id is required.");
        }

        if (members is null || members.Count == 0)
        {
            throw new DomainValidationException("A group arrival must contain at least one guest.");
        }

        if (members.Any(m => m is null))
        {
            throw new DomainValidationException("A group arrival cannot contain a null guest.");
        }

        GroupId = groupId;
        Members = members.ToArray();
    }

    public Guid GroupId { get; }

    public IReadOnlyList<Guest> Members { get; }

    public int Size => Members.Count;

    /// <summary>
    /// Creates an arrival of <paramref name="size"/> freshly generated guests
    /// under a new group identity.
    /// </summary>
    public static GroupArrival OfSize(int size)
    {
        if (size < 1)
        {
            throw new DomainValidationException("A group must contain at least one guest.");
        }

        var members = Enumerable.Range(0, size).Select(_ => Guest.CreateNew()).ToArray();
        return new GroupArrival(Guid.NewGuid(), members);
    }
}
