using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// Aggregate root for a single ride's single-riders line (ADR-0003): individual
/// guests, in arrival order, who consented to being seated beside a stranger. It is
/// deliberately separate from <see cref="RideQueue"/> — a group of one is a party,
/// not a single rider. All mutation goes through intent-revealing methods; a
/// per-ride lock protects ordering against concurrent filling and boarding.
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
            throw new DomainValidationException("Maximum single-riders line length must be at least one.");
        }

        RideId = rideId;
        MaxPeople = maxPeople;
    }

    public Guid RideId { get; private set; }

    /// <summary>The maximum number of individuals that may wait in this line at once.</summary>
    public int MaxPeople { get; private set; }

    /// <summary>The number of individuals currently waiting.</summary>
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
    /// exceeding <see cref="MaxPeople"/>. A count of zero or less never fits.
    /// </summary>
    public bool CanAccept(int count)
    {
        lock (_gate)
        {
            return count >= 1 && _people.Count + count <= MaxPeople;
        }
    }

    /// <summary>
    /// Appends <paramref name="person"/> to the back of the line and marks the
    /// aggregate <see cref="DomainModelState.Modified"/>.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// The line is already at <see cref="MaxPeople"/>; the people waiting are left
    /// untouched.
    /// </exception>
    public void Enqueue(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        lock (_gate)
        {
            if (_people.Count >= MaxPeople)
            {
                throw new DomainValidationException(
                    $"Ride {RideId} single-riders line is full: the maximum is {MaxPeople}.");
            }

            _people.AddLast(person);
            MarkChanged();
        }
    }

    /// <summary>
    /// Removes and returns up to <paramref name="count"/> individuals from the front
    /// of the line, in arrival order, in one locked step. Returns fewer when the line
    /// is shorter and an empty list for an empty line or a count of zero or less. The
    /// aggregate is marked changed only when someone was actually removed.
    /// </summary>
    public IReadOnlyList<Person> TakeUpTo(int count)
    {
        if (count <= 0)
        {
            return [];
        }

        lock (_gate)
        {
            var take = Math.Min(count, _people.Count);
            if (take == 0)
            {
                return [];
            }

            var taken = new Person[take];
            for (var i = 0; i < take; i++)
            {
                taken[i] = _people.First!.Value;
                _people.RemoveFirst();
            }

            MarkChanged();
            return taken;
        }
    }

    /// <summary>
    /// Returns <paramref name="people"/> — individuals taken but not seated — to the
    /// front of the line in their original relative order, so they keep their place.
    /// Capacity is not enforced: these guests were already admitted to the line.
    /// </summary>
    public void RequeueFront(IReadOnlyList<Person> people)
    {
        ArgumentNullException.ThrowIfNull(people);

        if (people.Count == 0)
        {
            return;
        }

        // Validate the whole batch first so a bad entry never half-applies it.
        if (people.Any(person => person is null))
        {
            throw new DomainValidationException("A requeued person cannot be null.");
        }

        lock (_gate)
        {
            // Walk backwards so each AddFirst leaves the batch in its original order.
            for (var i = people.Count - 1; i >= 0; i--)
            {
                _people.AddFirst(people[i]);
            }

            MarkChanged();
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
}
