namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A contiguous group of guests occupying a position in a ride's queue. Members
/// stay together and in order so the group can later be boarded as a unit. Owned
/// and ordered by <see cref="RideQueue"/>; created from a validated
/// <see cref="GroupArrival"/>.
/// </summary>
public sealed class QueuedGroup
{
    private readonly List<Guest> _members;

    internal QueuedGroup(GroupArrival arrival)
    {
        ArgumentNullException.ThrowIfNull(arrival);

        GroupId = arrival.GroupId;
        _members = [.. arrival.Members];
    }

    public Guid GroupId { get; private set; }

    public IReadOnlyList<Guest> Members => _members;

    public int Size => _members.Count;
}
