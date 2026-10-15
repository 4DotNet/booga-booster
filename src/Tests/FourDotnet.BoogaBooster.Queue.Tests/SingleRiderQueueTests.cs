using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

public class SingleRiderQueueTests
{
    private static SingleRiderQueue NewQueue(int maxPeople = 10) => new(Guid.NewGuid(), maxPeople);

    private static SingleRiderQueue Filled(int count, int maxPeople = 10)
    {
        var queue = NewQueue(maxPeople);
        for (var n = 1; n <= count; n++)
        {
            queue.Enqueue(QueueTestData.Person(number: n));
        }

        return queue;
    }

    private static long[] Numbers(IEnumerable<Person> people) => [.. people.Select(p => p.Number)];

    [Fact]
    public void Enqueue_PreservesArrivalOrder()
    {
        var queue = Filled(3);

        Assert.Equal([1L, 2L, 3L], Numbers(queue.SnapshotPeople()));
        Assert.Equal(3, queue.PeopleWaiting);
    }

    [Fact]
    public void Enqueue_WhenFull_ThrowsAndLeavesLineUntouched()
    {
        var queue = Filled(2, maxPeople: 2);

        Assert.Throws<DomainValidationException>(() => queue.Enqueue(QueueTestData.Person(number: 3)));

        Assert.Equal([1L, 2L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void Enqueue_RejectsNull() =>
        Assert.Throws<ArgumentNullException>(() => NewQueue().Enqueue(null!));

    [Fact]
    public void CanAccept_ReflectsRemainingCapacity()
    {
        var queue = Filled(2, maxPeople: 3);

        Assert.True(queue.CanAccept(1));
        Assert.False(queue.CanAccept(2));
        Assert.False(queue.CanAccept(0));
    }

    [Fact]
    public void TakeUpTo_FewerThanWaiting_TakesFromFrontAndKeepsRest()
    {
        var queue = Filled(5);

        var taken = queue.TakeUpTo(3);

        Assert.Equal([1L, 2L, 3L], Numbers(taken));
        Assert.Equal([4L, 5L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void TakeUpTo_MoreThanWaiting_TakesEveryoneAndEmptiesLine()
    {
        var queue = Filled(2);

        var taken = queue.TakeUpTo(5);

        Assert.Equal([1L, 2L], Numbers(taken));
        Assert.Equal(0, queue.PeopleWaiting);
    }

    [Fact]
    public void TakeUpTo_EmptyLine_ReturnsNobodyWithoutChange()
    {
        var queue = NewQueue();

        Assert.Empty(queue.TakeUpTo(3));
        Assert.Equal(0, queue.PeopleWaiting);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void TakeUpTo_NonPositiveCount_TakesNobody(int count)
    {
        var queue = Filled(2);

        Assert.Empty(queue.TakeUpTo(count));
        Assert.Equal(2, queue.PeopleWaiting);
    }

    [Fact]
    public void RequeueFront_RestoresOriginalOrderAtTheHead()
    {
        var queue = Filled(3);
        var taken = queue.TakeUpTo(2);

        queue.RequeueFront(taken);

        Assert.Equal([1L, 2L, 3L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_PlacesPeopleBeforeThoseStillWaiting()
    {
        var queue = Filled(3);
        var taken = queue.TakeUpTo(1);
        queue.Enqueue(QueueTestData.Person(number: 4));

        queue.RequeueFront(taken);

        Assert.Equal([1L, 2L, 3L, 4L], Numbers(queue.SnapshotPeople()));
    }

    [Fact]
    public void RequeueFront_EmptyList_IsANoOp()
    {
        var queue = Filled(1);

        queue.RequeueFront([]);

        Assert.Equal(1, queue.PeopleWaiting);
        Assert.Throws<ArgumentNullException>(() => queue.RequeueFront(null!));
    }

    [Fact]
    public void Constructor_ValidatesArguments()
    {
        Assert.Throws<DomainValidationException>(() => new SingleRiderQueue(Guid.Empty, 5));
        Assert.Throws<DomainValidationException>(() => new SingleRiderQueue(Guid.NewGuid(), 0));
    }
}
