using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// Aggregate root for a single ride's single-riders line (ADR-0003): individual
/// guests, each waiting on their own, who agreed to be seated next to a stranger
/// and are used to fill the seats a group-consistent load leaves free. It is kept
/// apart from <see cref="RideQueue"/> on purpose — a <em>group of one</em> in the
/// regular queue never consented to sharing a gondola, so the two are different
/// types rather than one line with a flag. Holds people in arrival order; all
/// mutation goes through intent-revealing methods, and a per-ride lock protects
/// the ordering against concurrent filling, boarding and reads.
/// </summary>
public sealed class SingleRiderQueue : DomainModel
{
    private readonly Lock _gate = new();
    private readonly LinkedList<Person> _people = new();

    public SingleRiderQueue(Guid rideId, int maxPeople)
        : base(isNew: true)
    {
        if (rideId == Guid.Empty)
        {
            throw new DomainValidationException("Ride id is required.");
        }

        if (maxPeople < 1)
        {
            throw new DomainValidationException("Maximum single-riders queue length must be at least one.");
        }

        RideId = rideId;
        MaxPeople = maxPeople;
    }

    public Guid RideId { get; private set; }

    /// <summary>The maximum number of individuals that may wait in this line at once.</summary>
    public int MaxPeople { get; private set; }

    /// <summary>How many individuals are waiting right now.</summary>
    public int PeopleWaiting
    {
        get
        {
            lock (_gate)
            {
                return _people.Count;
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="count"/> more individuals would still fit without
    /// exceeding <see cref="MaxPeople"/>.
    /// </summary>
    public bool CanAccept(int count)
    {
        lock (_gate)
        {
            return CanAcceptCore(count);
        }
    }

    /// <summary>
    /// Appends <paramref name="person"/> to the back of the line and marks the
    /// aggregate <see cref="DomainModelState.Modified"/>.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// The line is at <see cref="MaxPeople"/>. The people already waiting are left
    /// untouched.
    /// </exception>
    public void Enqueue(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        lock (_gate)
        {
            if (!CanAcceptCore(1))
            {
                throw new DomainValidationException(
                    $"Ride {RideId} single-riders queue is full: the maximum of {MaxPeople} is already waiting.");
            }

            _people.AddLast(person);
            MarkChanged();
        }
    }

    /// <summary>
    /// Removes and returns up to <paramref name="count"/> individuals from the front
    /// of the line, in arrival order — fewer when the line is shorter, and nobody for
    /// an empty line or a <paramref name="count"/> of zero or less. The whole take is
    /// one locked step, so a concurrent arrival can never land between two of the
    /// people taken. The aggregate is marked
    /// <see cref="DomainModelState.Modified"/> only when somebody was actually removed.
    /// </summary>
    public IReadOnlyList<Person> TakeUpTo(int count)
    {
        lock (_gate)
        {
            var take = Math.Min(count, _people.Count);
            if (take <= 0)
            {
                MarkChanged(changed: false);
                return [];
            }

            var taken = new Person[take];
            for (var i = 0; i < take; i++)
            {
                // The loop bound guarantees a first node on every iteration.
                var first = _people.First!;
                taken[i] = first.Value;
                _people.RemoveFirst();
            }

            MarkChanged();
            return taken;
        }
    }

    /// <summary>
    /// Returns <paramref name="people"/> to the <em>front</em> of the line, ahead of
    /// everyone still waiting and in the relative order given, so individuals who
    /// were taken but could not be seated keep their place. Returning guests are not
    /// new arrivals: they are re-admitted even when the line has since filled to
    /// <see cref="MaxPeople"/>, because refusing them would lose a guest.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// One of the people is <c>null</c>. Nobody is requeued in that case.
    /// </exception>
    public void RequeueFront(IReadOnlyList<Person> people)
    {
        ArgumentNullException.ThrowIfNull(people);

        foreach (var person in people)
        {
            if (person is null)
            {
                throw new DomainValidationException("A requeued person cannot be null.");
            }
        }

        lock (_gate)
        {
            // Walk backwards so the first person given ends up at the very front.
            for (var i = people.Count - 1; i >= 0; i--)
            {
                _people.AddFirst(people[i]);
            }

            MarkChanged(people.Count > 0);
        }
    }

    /// <summary>Returns the individuals currently waiting, in arrival order.</summary>
    public IReadOnlyList<Person> SnapshotPeople()
    {
        lock (_gate)
        {
            return [.. _people];
        }
    }

    private bool CanAcceptCore(int count)
    {
        if (count < 1)
        {
            return false;
        }

        // Subtract rather than add so a huge count cannot overflow past the maximum.
        return count <= MaxPeople - _people.Count;
    }
}
