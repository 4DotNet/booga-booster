using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the validation invariants of the Queue domain value objects and
/// aggregate constructors (ADR-0003).
/// </summary>
public class QueueDomainTests
{
    [Fact]
    public void Guest_WithEmptyId_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Guest(Guid.Empty));
    }

    [Fact]
    public void Guest_CreateNew_HasIdentityAndStartsNew()
    {
        var guest = Guest.CreateNew();

        Assert.NotEqual(Guid.Empty, guest.Id);
        Assert.Equal(DomainModelState.New, guest.State);
    }

    [Fact]
    public void GroupArrival_WithEmptyGroupId_Throws()
    {
        var members = new[] { Guest.CreateNew() };

        Assert.Throws<DomainValidationException>(() => new GroupArrival(Guid.Empty, members));
    }

    [Fact]
    public void GroupArrival_WithNoMembers_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new GroupArrival(Guid.NewGuid(), []));
    }

    [Fact]
    public void GroupArrival_WithNullMembers_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new GroupArrival(Guid.NewGuid(), null!));
    }

    [Fact]
    public void GroupArrival_WithNullGuest_Throws()
    {
        var members = new Guest[] { Guest.CreateNew(), null! };

        Assert.Throws<DomainValidationException>(() => new GroupArrival(Guid.NewGuid(), members));
    }

    [Fact]
    public void GroupArrival_OfSize_BelowOne_Throws()
    {
        Assert.Throws<DomainValidationException>(() => GroupArrival.OfSize(0));
    }

    [Fact]
    public void GroupArrival_OfSize_CreatesDistinctGuestsUnderOneGroup()
    {
        var arrival = GroupArrival.OfSize(3);

        Assert.Equal(3, arrival.Size);
        Assert.NotEqual(Guid.Empty, arrival.GroupId);
        Assert.Equal(3, arrival.Members.Select(m => m.Id).Distinct().Count());
    }

    [Fact]
    public void RideQueue_WithEmptyRideId_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new RideQueue(Guid.Empty, maxPeople: 10));
    }

    [Fact]
    public void RideQueue_WithNonPositiveMax_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new RideQueue(Guid.NewGuid(), maxPeople: 0));
    }

    [Fact]
    public void RideQueue_CanAccept_NonPositiveSize_IsFalse()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10);

        Assert.False(queue.CanAccept(0));
        Assert.False(queue.CanAccept(-3));
    }

    [Fact]
    public void RideQueue_Enqueue_NullArrival_Throws()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10);

        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }
}
