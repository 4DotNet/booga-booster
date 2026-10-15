namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A contiguous group of guests occupying a position in a ride's queue. Members
/// stay together and in order so the group can later be boarded as a unit. Owned
/// and ordered by <see cref="RideQueue"/>; created from a validated
/// <see cref="GroupArrival"/> together with the moment it joined the line.
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
    /// When the group joined the line, as read from the module's
    /// <see cref="TimeProvider"/> by the caller — the domain never reads a clock.
    /// </summary>
    public DateTimeOffset QueuedAt { get; private set; }

    public IReadOnlyList<Person> Members => _members;

    public int Size => _members.Count;

    /// <summary>The combined weight of everyone in the group, in kilograms.</summary>
    public int TotalWeightInKilograms => _members.Sum(m => m.WeightInKilograms);

    /// <summary>
    /// How long the group has been waiting at <paramref name="now"/>. Never negative:
    /// a reading taken before the enqueue time (clock skew) counts as no wait at all.
    /// </summary>
    public TimeSpan WaitingTimeAt(DateTimeOffset now)
    {
        var waited = now - QueuedAt;
        return waited > TimeSpan.Zero ? waited : TimeSpan.Zero;
    }
}
