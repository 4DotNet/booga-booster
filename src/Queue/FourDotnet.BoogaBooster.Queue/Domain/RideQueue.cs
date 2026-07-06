using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// Aggregate root for a single ride's waiting line (ADR-0003). Holds groups in
/// arrival order and preserves each group's membership so members stay adjacent
/// and board together. All mutation goes through intent-revealing methods; a
/// per-ride lock protects ordering against concurrent filling and reads.
/// </summary>
public sealed class RideQueue : DomainModel
{
    private readonly Lock _gate = new();
    private readonly LinkedList<QueuedGroup> _groups = new();

    public RideQueue(Guid rideId, int maxPeople)
        : base(isNew: true)
    {
        if (rideId == Guid.Empty)
        {
            throw new DomainValidationException("Ride id is required.");
        }

        if (maxPeople < 1)
        {
            throw new DomainValidationException("Maximum queue length must be at least one.");
        }

        RideId = rideId;
        MaxPeople = maxPeople;
    }

    public Guid RideId { get; private set; }

    /// <summary>The maximum number of people that may wait in this queue at once.</summary>
    public int MaxPeople { get; private set; }

    public int GroupCount
    {
        get
        {
            lock (_gate)
            {
                return _groups.Count;
            }
        }
    }

    public int PeopleWaiting
    {
        get
        {
            lock (_gate)
            {
                return _groups.Sum(g => g.Size);
            }
        }
    }

    /// <summary>
    /// Whether a group of <paramref name="size"/> people would still fit without
    /// exceeding <see cref="MaxPeople"/>.
    /// </summary>
    public bool CanAccept(int size)
    {
        lock (_gate)
        {
            return CanAcceptCore(size);
        }
    }

    /// <summary>
    /// Appends <paramref name="arrival"/> as a single contiguous group at the back
    /// of the queue and marks the aggregate <see cref="DomainModelState.Modified"/>.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// Adding the group would exceed <see cref="MaxPeople"/>.
    /// </exception>
    public QueuedGroup Enqueue(GroupArrival arrival)
    {
        ArgumentNullException.ThrowIfNull(arrival);

        lock (_gate)
        {
            if (!CanAcceptCore(arrival.Size))
            {
                throw new DomainValidationException(
                    $"Ride {RideId} queue is full: {arrival.Size} would exceed the maximum of {MaxPeople}.");
            }

            var group = new QueuedGroup(arrival);
            _groups.AddLast(group);
            MarkChanged();
            return group;
        }
    }

    /// <summary>Returns the group at the front of the queue without removing it, or null when empty.</summary>
    public QueuedGroup? PeekNextGroup()
    {
        lock (_gate)
        {
            return _groups.First?.Value;
        }
    }

    /// <summary>
    /// Removes and returns the group identified by <paramref name="groupId"/> from
    /// anywhere in the line, preserving the arrival order of the groups around it,
    /// and marks the aggregate <see cref="DomainModelState.Modified"/>. Returns
    /// <c>null</c> when no such group is waiting — for example it was already taken.
    /// Removing by identity (rather than by position) keeps a caller that chose a
    /// group from a stale snapshot race-safe against concurrent filling.
    /// </summary>
    public QueuedGroup? Remove(Guid groupId)
    {
        lock (_gate)
        {
            for (var node = _groups.First; node is not null; node = node.Next)
            {
                if (node.Value.GroupId == groupId)
                {
                    _groups.Remove(node);
                    MarkChanged();
                    return node.Value;
                }
            }

            return null;
        }
    }

    /// <summary>Returns the groups currently waiting, in arrival order.</summary>
    public IReadOnlyList<QueuedGroup> SnapshotGroups()
    {
        lock (_gate)
        {
            return [.. _groups];
        }
    }

    private bool CanAcceptCore(int size)
    {
        if (size < 1)
        {
            return false;
        }

        return _groups.Sum(g => g.Size) + size <= MaxPeople;
    }
}
