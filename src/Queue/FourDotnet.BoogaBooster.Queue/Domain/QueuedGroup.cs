namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A contiguous group of guests occupying a position in a ride's queue. Members
/// stay together and in order so the group can later be boarded as a unit. Owned
/// and ordered by <see cref="RideQueue"/>; created from a validated
/// <see cref="GroupArrival"/>, stamped with the moment it joined the line, and
/// handed the queue's <see cref="GrumpinessPolicy"/> so it can report how the wait
/// has worn on its members without ever mutating them.
/// </summary>
public sealed class QueuedGroup
{
    private readonly List<Person> _members;
    private readonly GrumpinessPolicy _grumpinessPolicy;

    internal QueuedGroup(GroupArrival arrival, DateTimeOffset queuedAt, GrumpinessPolicy grumpinessPolicy)
    {
        ArgumentNullException.ThrowIfNull(arrival);
        ArgumentNullException.ThrowIfNull(grumpinessPolicy);

        GroupId = arrival.GroupId;
        QueuedAt = queuedAt;
        _members = [.. arrival.Members];
        _grumpinessPolicy = grumpinessPolicy;
    }

    public Guid GroupId { get; private set; }

    /// <summary>The moment the group joined the line, as stamped by the enqueuing caller.</summary>
    public DateTimeOffset QueuedAt { get; private set; }

    public IReadOnlyList<Person> Members => _members;

    public int Size => _members.Count;

    /// <summary>The combined weight of everyone in the group, in kilograms.</summary>
    public int TotalWeightInKilograms => _members.Sum(m => m.WeightInKilograms);

    /// <summary>
    /// How long the group has been waiting as of <paramref name="now"/>. Never
    /// negative: a clock that reads earlier than <see cref="QueuedAt"/> counts as no
    /// wait at all.
    /// </summary>
    public TimeSpan WaitedFor(DateTimeOffset now)
    {
        var waited = now - QueuedAt;
        return waited < TimeSpan.Zero ? TimeSpan.Zero : waited;
    }

    /// <summary>
    /// The happiness <paramref name="member"/> reports as of <paramref name="now"/>:
    /// their arrival happiness eroded by the group's wait under the queue's
    /// <see cref="GrumpinessPolicy"/>. Derived, never written back to the person.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="member"/> is not in this group.</exception>
    public double CurrentHappiness(Person member, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(member);

        if (!_members.Contains(member))
        {
            throw new ArgumentException($"Person {member.Number} is not a member of group {GroupId}.", nameof(member));
        }

        return _grumpinessPolicy.CurrentHappiness(member.Profile.Happiness, WaitedFor(now));
    }

    /// <summary>
    /// The mean current happiness of the group's members as of <paramref name="now"/>.
    /// A group is never empty, so there is always a value.
    /// </summary>
    public double AverageHappiness(DateTimeOffset now)
    {
        // Every member shares the group's wait, so compute it once for all of them.
        var waited = WaitedFor(now);
        var total = 0.0;

        foreach (var member in _members)
        {
            total += _grumpinessPolicy.CurrentHappiness(member.Profile.Happiness, waited);
        }

        return total / _members.Count;
    }
}
