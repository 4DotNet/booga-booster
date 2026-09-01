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

    /// <summary>
    /// Splits a headcount of <paramref name="totalSize"/> into the smallest number
    /// of boardable group sizes, none larger than <paramref name="maxGroupSize"/>
    /// and each as close to the others in size as possible. A headcount already
    /// within the limit yields a single group, so groups that can board are never
    /// broken up. Sizes are returned largest-first and always sum to
    /// <paramref name="totalSize"/>.
    /// </summary>
    /// <remarks>
    /// A group larger than a ride's total seat capacity can never satisfy the
    /// fully-fits boarding rule, so it would wait in the line forever. Splitting it
    /// on arrival keeps every waiting group boardable. Dividing evenly (33 becomes
    /// 17 and 16, not 32 and 1) avoids stranding a near-empty remainder group.
    /// </remarks>
    /// <exception cref="DomainValidationException">
    /// <paramref name="totalSize"/> is less than one, or
    /// <paramref name="maxGroupSize"/> is less than one.
    /// </exception>
    public static IReadOnlyList<int> PartitionSizes(int totalSize, int maxGroupSize)
    {
        if (totalSize < 1)
        {
            throw new DomainValidationException("A group must contain at least one person.");
        }

        if (maxGroupSize < 1)
        {
            throw new DomainValidationException("The maximum boardable group size must be at least one.");
        }

        if (totalSize <= maxGroupSize)
        {
            return [totalSize];
        }

        var groupCount = (totalSize + maxGroupSize - 1) / maxGroupSize;
        var baseSize = totalSize / groupCount;
        var remainder = totalSize % groupCount;

        var sizes = new int[groupCount];
        for (var i = 0; i < groupCount; i++)
        {
            // Spread the remainder one person at a time over the leading groups so
            // no two groups differ by more than one.
            sizes[i] = i < remainder ? baseSize + 1 : baseSize;
        }

        return sizes;
    }
}
