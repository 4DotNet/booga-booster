using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// Aggregate root for a single ride's single-riders line (ADR-0003): individual
/// guests who accept being seated beside a stranger, held in arrival order. Kept
/// apart from <see cref="RideQueue"/> so a group of one (a party that boards as a
/// group) is never confused with a single rider. Individuals are interchangeable,
/// so they are taken in bulk from the front rather than by identity; a per-ride
/// lock keeps every operation atomic against the concurrent filler.
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

    /// <summary>Whether <paramref name="count"/> more individuals would still fit within <see cref="MaxPeople"/>.</summary>
    public bool CanAccept(int count)
    {
        lock (_gate)
        {
            return CanAcceptCore(count);
        }
    }

    /// <summary>Appends <paramref name="person"/> at the back of the line.</summary>
    /// <exception cref="DomainValidationException">The line is at <see cref="MaxPeople"/>.</exception>
    public void Enqueue(Person person)
    {
        ArgumentNullException.ThrowIfNull(person);

        lock (_gate)
        {
            if (!CanAcceptCore(1))
            {
                throw new DomainValidationException(
                    $"Ride {RideId} single-riders queue is full: the maximum is {MaxPeople}.");
            }

            _people.AddLast(person);
            MarkChanged();
        }
    }

    /// <summary>
    /// Removes and returns up to <paramref name="count"/> individuals from the front
    /// of the line in one atomic step — fewer when the line is shorter, none for a
    /// count of zero or less or an empty line. Marks the aggregate changed only when
    /// someone was actually removed.
    /// </summary>
    public IReadOnlyList<Person> TakeUpTo(int count)
    {
        lock (_gate)
        {
            var take = Math.Min(count, _people.Count);
            if (take <= 0)
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
    /// Returns <paramref name="people"/> to the head of the line, keeping their
    /// original relative order, so an unsuccessful boarding costs nobody their place.
    /// The line's capacity is deliberately not re-checked: these guests already held
    /// a place in it.
    /// </summary>
    public void RequeueFront(IReadOnlyList<Person> people)
    {
        ArgumentNullException.ThrowIfNull(people);

        if (people.Count == 0)
        {
            return;
        }

        lock (_gate)
        {
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

    private bool CanAcceptCore(int count) => count >= 1 && _people.Count + count <= MaxPeople;
}
