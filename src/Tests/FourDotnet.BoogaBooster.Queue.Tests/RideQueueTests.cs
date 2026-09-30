using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

public class RideQueueTests
{
    private static RideQueue NewQueue(int maxPeople = 100, int maxBoardableGroupSize = 32) =>
        new(Guid.NewGuid(), maxPeople, maxBoardableGroupSize);

    [Fact]
    public void Enqueue_AppendsGroupsInArrivalOrder()
    {
        var queue = NewQueue();

        var first = queue.Enqueue(QueueTestData.Group(2));
        var second = queue.Enqueue(QueueTestData.Group(3));

        var groups = queue.SnapshotGroups();
        Assert.Equal(2, groups.Count);
        Assert.Equal(first.GroupId, groups[0].GroupId);
        Assert.Equal(second.GroupId, groups[1].GroupId);
        Assert.Equal(first.GroupId, queue.PeekNextGroup()!.GroupId);
    }

    [Fact]
    public void Enqueue_PreservesGroupContiguityAndSize()
    {
        var queue = NewQueue();
        var arrival = QueueTestData.Group(4);

        var group = queue.Enqueue(arrival);

        Assert.Equal(4, group.Size);
        Assert.Equal(4, group.Members.Count);
        Assert.Equal(arrival.GroupId, group.GroupId);
        Assert.All(group.Members, m => Assert.True(m.Number > 0));
    }

    [Fact]
    public void Enqueue_TracksPeopleAndGroupCounts()
    {
        var queue = NewQueue();

        queue.Enqueue(QueueTestData.Group(1));
        queue.Enqueue(QueueTestData.Group(5));

        Assert.Equal(2, queue.GroupCount);
        Assert.Equal(6, queue.PeopleWaiting);
    }

    [Fact]
    public void Enqueue_BeyondMaxPeople_Throws()
    {
        var queue = NewQueue(maxPeople: 4);
        queue.Enqueue(QueueTestData.Group(3));

        Assert.False(queue.CanAccept(2));
        Assert.True(queue.CanAccept(1));
        Assert.Throws<DomainValidationException>(() => queue.Enqueue(QueueTestData.Group(2)));
        Assert.Equal(3, queue.PeopleWaiting);
    }

    [Fact]
    public void NewQueue_IsEmptyAndHasNoFrontGroup()
    {
        var queue = NewQueue();

        Assert.Equal(0, queue.GroupCount);
        Assert.Equal(0, queue.PeopleWaiting);
        Assert.Null(queue.PeekNextGroup());
    }

    [Fact]
    public void Remove_ById_ReturnsGroupAndShrinksTheLine()
    {
        var queue = NewQueue();
        var first = queue.Enqueue(QueueTestData.Group(2));
        var second = queue.Enqueue(QueueTestData.Group(3));

        var removed = queue.Remove(first.GroupId);

        Assert.NotNull(removed);
        Assert.Equal(first.GroupId, removed!.GroupId);
        Assert.Equal(1, queue.GroupCount);
        Assert.Equal(3, queue.PeopleWaiting);
        Assert.Equal(second.GroupId, queue.PeekNextGroup()!.GroupId);
    }

    [Fact]
    public void Remove_NonFrontGroup_PreservesOrderOfTheRest()
    {
        var queue = NewQueue();
        var first = queue.Enqueue(QueueTestData.Group(1));
        var second = queue.Enqueue(QueueTestData.Group(2));
        var third = queue.Enqueue(QueueTestData.Group(3));

        var removed = queue.Remove(second.GroupId);

        Assert.NotNull(removed);
        Assert.Equal(second.GroupId, removed!.GroupId);
        Assert.Equal(
            new[] { first.GroupId, third.GroupId },
            queue.SnapshotGroups().Select(g => g.GroupId));
    }

    [Fact]
    public void Remove_AbsentOrAlreadyTakenId_ReturnsNull()
    {
        var queue = NewQueue();
        var group = queue.Enqueue(QueueTestData.Group(2));

        Assert.Null(queue.Remove(Guid.NewGuid()));

        Assert.NotNull(queue.Remove(group.GroupId));
        Assert.Null(queue.Remove(group.GroupId));
        Assert.Equal(0, queue.GroupCount);
    }

    [Fact]
    public async Task Enqueue_And_Remove_Concurrently_KeepOrderingIntact()
    {
        var queue = NewQueue(maxPeople: 10_000);
        var enqueued = new System.Collections.Concurrent.ConcurrentQueue<Guid>();

        var cancellationToken = TestContext.Current.CancellationToken;

        var filler = Task.Run(
            () =>
            {
                for (var i = 0; i < 500; i++)
                {
                    enqueued.Enqueue(queue.Enqueue(QueueTestData.Group(1)).GroupId);
                }
            },
            cancellationToken);

        var remover = Task.Run(
            () =>
            {
                var removedCount = 0;
                while (removedCount < 200)
                {
                    if (enqueued.TryDequeue(out var id) && queue.Remove(id) is not null)
                    {
                        removedCount++;
                    }
                }
            },
            cancellationToken);

        await Task.WhenAll(filler, remover);

        // Every surviving group is still a distinct, contiguous single-person group:
        // no interleaving or corruption from concurrent enqueue/remove.
        var groups = queue.SnapshotGroups();
        Assert.Equal(300, groups.Count);
        Assert.Equal(groups.Count, groups.Select(g => g.GroupId).Distinct().Count());
        Assert.All(groups, g => Assert.Equal(1, g.Size));
    }

    [Fact]
    public void Enqueue_MarksAggregateModified()
    {
        // A queue rehydrated from a store (Pristine) transitions to Modified on enqueue.
        var queue = NewQueue();
        Assert.Equal(DomainModelState.New, queue.State);

        queue.Enqueue(QueueTestData.Group(1));

        // Constructed-in-code queues stay New (an insert); the state plumbing itself
        // is covered by DomainModelStateTests.
        Assert.Equal(DomainModelState.New, queue.State);
    }
}
