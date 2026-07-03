namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A contiguous group of guests occupying a position in a ride's queue. Members
/// stay together and in order so the group can later be boarded as a unit. Owned
/// and ordered by <see cref="RideQueue"/>; created from a validated
/// <see cref="GroupArrival"/>.
/// </summary>
public sealed class QueuedGroup
{
    private readonly List<Person> _members;

    internal QueuedGroup(GroupArrival arrival)
    {
        ArgumentNullException.ThrowIfNull(arrival);

        GroupId = arrival.GroupId;
        _members = [.. arrival.Members];
    }

    public Guid GroupId { get; private set; }

    public IReadOnlyList<Person> Members => _members;

    public int Size => _members.Count;

    /// <summary>The combined weight of everyone in the group, in kilograms.</summary>
    public int TotalWeightInKilograms => _members.Sum(m => m.WeightInKilograms);
}
