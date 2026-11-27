using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

public class SingleRiderQueueTests
{
    private static SingleRiderQueue NewQueue(int maxPeople = 100) => new(Guid.NewGuid(), maxPeople);

    private static Person[] People(params long[] numbers) =>
        [.. numbers.Select(n => QueueTestData.Person(number: n, name: $"Person {n}"))];

    private static SingleRiderQueue QueueWith(params long[] numbers)
    {
        var queue = NewQueue();
        foreach (var person in People(numbers))
        {
            queue.Enqueue(person);
        }

        return queue;
    }

    private static long[] Numbers(IEnumerable<Person> people) => [.. people.Select(p => p.Number)];

    [Fact]
    public void Constructor_WithEmptyRideId_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new SingleRiderQueue(Guid.Empty, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveCapacity_Throws(int maxPeople)
    {
        Assert.Throws<DomainValidationException>(() => new SingleRiderQueue(Guid.NewGuid(), maxPeople));
    }

    [Fact]
    public void Constructor_ExposesRideAndCapacity_AndStartsEmpty()
    {
        var rideId = Guid.NewGuid();

        var queue = new SingleRiderQueue(rideId, 7);

        Assert.Equal(rideId, queue.RideId);
        Assert.Equal(7, queue.MaxPeople);
        Assert.Equal(0, queue.PeopleWaiting);
        Assert.Empty(queue.SnapshotPeople());
        Assert.Equal(DomainModelState.New, queue.State);
    }

    [Fact]
    public void Enqueue_PreservesArrivalOrder()
    {
        var queue = QueueWith(1, 2, 3);

        Assert.Equal([1L, 2L, 3L], Numbers(queue.SnapshotPeople()));
        Assert.Equal(3, queue.PeopleWaiting);
    }

    [Fact]
    public void Enqueue_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewQueue().Enqueue(null!));
    }

    [Fact]
    public void Enqueue_AtCapacity_ThrowsAndLeavesTheLineUntouched()
    {
        var queue = new SingleRiderQueue(Guid.NewGuid(), maxPeople: 2);
        queue.Enqueue(QueueTestData.Person(number: 1));
        queue.Enqueue(QueueTestData.Person(number: 2));

        Assert.Throws<DomainValidationException>(() => queue.Enqueue(QueueTestData.Person(number: 3)));

        Assert.Equal([1L, 2L], Numbers(queue.SnapshotPeople()));
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 1, true)]
    [InlineData(2, 1, false)]
    [InlineData(0, 2, true)]
    [InlineData(0, 3, false)]
    [InlineData(0, 0, false)]
    [InlineData(0, -1, false)]
    public void CanAccept_RespectsCapacity(int waiting, int count, bool expected)
    {
        var queue = new SingleRiderQueue(Guid.NewGuid(), maxPeople: 2);
        for (var i = 1; i <= waiting; i++)
        {
            queue.Enqueue(QueueTestData.Person(number: i));
        }

        Assert.Equal(expected, queue.CanAccept(count));
    }

    [Fact]
    public void TakeUpTo_FewerThanWaiting_ReturnsTheFrontInOrder_AndKeepsTheRest()
    {
        var queue = QueueWith(1, 2, 3, 4, 5);

        var taken = queue.TakeUpTo(3);

        Assert.Equal([1L, 2L, 3L], Numbers(taken));
        Assert.Equal([4L, 5L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void TakeUpTo_MoreThanWaiting_ReturnsEveryone_AndEmptiesTheLine()
    {
        var queue = QueueWith(1, 2);

        var taken = queue.TakeUpTo(5);

        Assert.Equal([1L, 2L], Numbers(taken));
        Assert.Equal(0, queue.PeopleWaiting);
    }

    [Fact]
    public void TakeUpTo_FromAnEmptyLine_ReturnsNobody()
    {
        Assert.Empty(NewQueue().TakeUpTo(3));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void TakeUpTo_NonPositiveCount_TakesNobody(int count)
    {
        var queue = QueueWith(1, 2);

        Assert.Empty(queue.TakeUpTo(count));
        Assert.Equal([1L, 2L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_RestoresTakenPeopleAheadOfTheRest_InOriginalOrder()
    {
        var queue = QueueWith(1, 2, 3);
        var taken = queue.TakeUpTo(2);

        queue.RequeueFront(taken);

        Assert.Equal([1L, 2L, 3L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_IgnoresCapacity_BecauseThePeopleWereAlreadyAdmitted()
    {
        var queue = new SingleRiderQueue(Guid.NewGuid(), maxPeople: 1);
        queue.Enqueue(QueueTestData.Person(number: 3));

        queue.RequeueFront(People(1, 2));

        Assert.Equal([1L, 2L, 3L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_WithAnEmptyList_ChangesNothing()
    {
        var queue = QueueWith(1);

        queue.RequeueFront([]);

        Assert.Equal([1L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_WithANullEntry_ThrowsAndChangesNothing()
    {
        var queue = QueueWith(3);
        Person[] batch = [QueueTestData.Person(number: 1), null!];

        Assert.Throws<DomainValidationException>(() => queue.RequeueFront(batch));

        Assert.Equal([3L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => NewQueue().RequeueFront(null!));
    }

    [Fact]
    public void SnapshotPeople_IsACopy_UnaffectedByLaterChanges()
    {
        var queue = QueueWith(1, 2);

        var snapshot = queue.SnapshotPeople();
        queue.TakeUpTo(2);

        Assert.Equal([1L, 2L], Numbers(snapshot));
    }
}
