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

    internal QueuedGroup(GroupArrival arrival, DateTimeOffset queuedAt)
    {
        ArgumentNullException.ThrowIfNull(arrival);

        GroupId = arrival.GroupId;
        QueuedAt = queuedAt;
        _members = [.. arrival.Members];
    }

    public Guid GroupId { get; private set; }

    /// <summary>
    /// When the group joined the line. Every member's wait, and so their current
    /// happiness (<see cref="Person.HappinessAfterWaiting"/>), is measured from here.
    /// </summary>
    public DateTimeOffset QueuedAt { get; private set; }

    public IReadOnlyList<Person> Members => _members;

    public int Size => _members.Count;

    /// <summary>The combined weight of everyone in the group, in kilograms.</summary>
    public int TotalWeightInKilograms => _members.Sum(m => m.WeightInKilograms);
}
