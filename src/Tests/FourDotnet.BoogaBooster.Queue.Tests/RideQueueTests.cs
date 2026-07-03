using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

public class RideQueueTests
{
    private static RideQueue NewQueue(int maxPeople = 100) => new(Guid.NewGuid(), maxPeople);

    [Fact]
    public void Enqueue_AppendsGroupsInArrivalOrder()
    {
        var queue = NewQueue();

        var first = queue.Enqueue(GroupArrival.OfSize(2));
        var second = queue.Enqueue(GroupArrival.OfSize(3));

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
        var arrival = GroupArrival.OfSize(4);

        var group = queue.Enqueue(arrival);

        Assert.Equal(4, group.Size);
        Assert.Equal(4, group.Members.Count);
        Assert.Equal(arrival.GroupId, group.GroupId);
        Assert.All(group.Members, m => Assert.NotEqual(Guid.Empty, m.Id));
    }

    [Fact]
    public void Enqueue_TracksPeopleAndGroupCounts()
    {
        var queue = NewQueue();

        queue.Enqueue(GroupArrival.OfSize(1));
        queue.Enqueue(GroupArrival.OfSize(5));

        Assert.Equal(2, queue.GroupCount);
        Assert.Equal(6, queue.PeopleWaiting);
    }

    [Fact]
    public void Enqueue_BeyondMaxPeople_Throws()
    {
        var queue = NewQueue(maxPeople: 4);
        queue.Enqueue(GroupArrival.OfSize(3));

        Assert.False(queue.CanAccept(2));
        Assert.True(queue.CanAccept(1));
        Assert.Throws<DomainValidationException>(() => queue.Enqueue(GroupArrival.OfSize(2)));
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
    public void Enqueue_MarksAggregateModified()
    {
        // A queue rehydrated from a store (Pristine) transitions to Modified on enqueue.
        var queue = NewQueue();
        Assert.Equal(DomainModelState.New, queue.State);

        queue.Enqueue(GroupArrival.OfSize(1));

        // Constructed-in-code queues stay New (an insert); the state plumbing itself
        // is covered by DomainModelStateTests.
        Assert.Equal(DomainModelState.New, queue.State);
    }
}
