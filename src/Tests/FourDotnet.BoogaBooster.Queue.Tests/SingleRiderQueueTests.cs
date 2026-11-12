using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the <see cref="SingleRiderQueue"/> aggregate: arrival order, the bulk
/// take from the front, the line's own capacity, returning un-seated individuals to
/// the front, and validation on construction.
/// </summary>
public sealed class SingleRiderQueueTests
{
    private static SingleRiderQueue NewQueue(int maxPeople = 100) => new(Guid.NewGuid(), maxPeople);

    /// <summary>Enqueues <paramref name="count"/> people numbered 1..count and returns them in arrival order.</summary>
    private static Person[] EnqueuePeople(SingleRiderQueue queue, int count)
    {
        var people = Enumerable.Range(1, count)
            .Select(number => QueueTestData.Person(number, $"Single {number}"))
            .ToArray();

        foreach (var person in people)
        {
            queue.Enqueue(person);
        }

        return people;
    }

    private static long[] Numbers(IEnumerable<Person> people) => [.. people.Select(p => p.Number)];

    // Construction.

    [Fact]
    public void Constructor_KeepsTheRideAndTheCapacity_AndStartsEmptyAndNew()
    {
        var rideId = Guid.NewGuid();

        var queue = new SingleRiderQueue(rideId, maxPeople: 25);

        Assert.Equal(rideId, queue.RideId);
        Assert.Equal(25, queue.MaxPeople);
        Assert.Equal(0, queue.PeopleWaiting);
        Assert.Empty(queue.SnapshotPeople());
        Assert.Equal(DomainModelState.New, queue.State);
    }

    [Fact]
    public void Constructor_WithEmptyRideId_ThrowsDomainValidationException()
    {
        var exception = Assert.Throws<DomainValidationException>(() => new SingleRiderQueue(Guid.Empty, 10));

        Assert.Contains("Ride id", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveMaxPeople_ThrowsDomainValidationException(int maxPeople)
    {
        var exception = Assert.Throws<DomainValidationException>(
            () => new SingleRiderQueue(Guid.NewGuid(), maxPeople));

        Assert.Contains("at least one", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    // Arrival order.

    [Fact]
    public void Enqueue_KeepsIndividualsInArrivalOrder()
    {
        var queue = NewQueue();

        var people = EnqueuePeople(queue, 3);

        Assert.Equal(3, queue.PeopleWaiting);
        Assert.Equal(Numbers(people), Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void Enqueue_NullPerson_Throws()
    {
        var queue = NewQueue();

        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
        Assert.Equal(0, queue.PeopleWaiting);
    }

    [Fact]
    public void SnapshotPeople_IsDetachedFromLaterChanges()
    {
        var queue = NewQueue();
        EnqueuePeople(queue, 2);

        var snapshot = queue.SnapshotPeople();
        queue.TakeUpTo(1);

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(1, queue.PeopleWaiting);
    }

    // Bulk take.

    [Fact]
    public void TakeUpTo_FewerThanWaiting_ReturnsTheFront_AndLeavesTheRestInOrder()
    {
        var queue = NewQueue();
        var people = EnqueuePeople(queue, 5);

        var taken = queue.TakeUpTo(3);

        Assert.Equal(Numbers(people[..3]), Numbers(taken));
        Assert.Equal(Numbers(people[3..]), Numbers(queue.SnapshotPeople()));
        Assert.Equal(2, queue.PeopleWaiting);
    }

    [Fact]
    public void TakeUpTo_MoreThanWaiting_ReturnsEveryone_AndEmptiesTheLine()
    {
        var queue = NewQueue();
        var people = EnqueuePeople(queue, 2);

        var taken = queue.TakeUpTo(5);

        Assert.Equal(Numbers(people), Numbers(taken));
        Assert.Equal(0, queue.PeopleWaiting);
        Assert.Empty(queue.SnapshotPeople());
    }

    [Fact]
    public void TakeUpTo_FromAnEmptyLine_ReturnsNobody_WithoutThrowing()
    {
        var queue = NewQueue();

        var taken = queue.TakeUpTo(4);

        Assert.Empty(taken);
        Assert.Equal(0, queue.PeopleWaiting);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void TakeUpTo_ZeroOrFewer_TakesNobody_AndLeavesTheLineUnchanged(int count)
    {
        var queue = NewQueue();
        var people = EnqueuePeople(queue, 3);

        var taken = queue.TakeUpTo(count);

        Assert.Empty(taken);
        Assert.Equal(Numbers(people), Numbers(queue.SnapshotPeople()));
    }

    // Capacity.

    [Fact]
    public void CanAccept_TracksTheRemainingRoom()
    {
        var queue = NewQueue(maxPeople: 3);
        EnqueuePeople(queue, 2);

        Assert.True(queue.CanAccept(1));
        Assert.False(queue.CanAccept(2));
        Assert.False(queue.CanAccept(int.MaxValue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void CanAccept_ZeroOrFewer_IsFalse(int count)
    {
        Assert.False(NewQueue().CanAccept(count));
    }

    [Fact]
    public void Enqueue_AtMaxPeople_Throws_AndLeavesTheWaitingPeopleUntouched()
    {
        var queue = NewQueue(maxPeople: 2);
        var people = EnqueuePeople(queue, 2);

        var exception = Assert.Throws<DomainValidationException>(
            () => queue.Enqueue(QueueTestData.Person(number: 99)));

        Assert.Contains("full", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(Numbers(people), Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void Enqueue_AfterATakeFreedRoom_IsAcceptedAgain()
    {
        var queue = NewQueue(maxPeople: 2);
        EnqueuePeople(queue, 2);
        queue.TakeUpTo(1);

        queue.Enqueue(QueueTestData.Person(number: 99));

        Assert.Equal([2L, 99L], Numbers(queue.SnapshotPeople()));
    }

    // Requeue.

    [Fact]
    public void RequeueFront_PutsThePeopleBackAhead_InTheirOriginalRelativeOrder()
    {
        var queue = NewQueue();
        var people = EnqueuePeople(queue, 3); // A, B, C
        var taken = queue.TakeUpTo(2);        // A, B — C is still waiting

        queue.RequeueFront(taken);

        Assert.Equal(Numbers(people), Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_OntoAnEmptyLine_RestoresTheOrder()
    {
        var queue = NewQueue();
        var people = EnqueuePeople(queue, 3);
        var taken = queue.TakeUpTo(3);

        queue.RequeueFront(taken);

        Assert.Equal(Numbers(people), Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_ReadmitsReturningGuests_EvenWhenTheLineHasSinceFilled()
    {
        var queue = NewQueue(maxPeople: 2);
        EnqueuePeople(queue, 2);                             // 1, 2
        var taken = queue.TakeUpTo(1);                       // 1 leaves to board
        queue.Enqueue(QueueTestData.Person(number: 3));      // a new arrival fills the line

        queue.RequeueFront(taken);

        // Refusing a returning guest would lose them; they keep their place instead.
        Assert.Equal([1L, 2L, 3L], Numbers(queue.SnapshotPeople()));
        Assert.False(queue.CanAccept(1));
    }

    [Fact]
    public void RequeueFront_WithNobody_LeavesTheLineUnchanged()
    {
        var queue = NewQueue();
        var people = EnqueuePeople(queue, 2);

        queue.RequeueFront([]);

        Assert.Equal(Numbers(people), Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_WithANullPerson_Throws_AndRequeuesNobody()
    {
        var queue = NewQueue();
        var people = EnqueuePeople(queue, 1);

        Assert.Throws<DomainValidationException>(
            () => queue.RequeueFront([QueueTestData.Person(number: 50), null!]));

        Assert.Equal(Numbers(people), Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_NullList_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewQueue().RequeueFront(null!));
    }
}
