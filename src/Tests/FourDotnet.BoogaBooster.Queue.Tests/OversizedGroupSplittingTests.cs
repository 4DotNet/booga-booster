using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the rule that keeps every waiting group boardable: a group is only ever
/// boarded when it fully fits, so a party larger than the ride's total seat
/// capacity (32 — sixteen gondolas of two) is split on arrival into the smallest
/// number of evenly sized boardable groups. Parties within capacity are never
/// broken up.
/// </summary>
public class OversizedGroupSplittingTests
{
    private const int RideSeatCapacity = 32;

    private static (RideQueueService service, Mock<IIntegrationEventPublisher> publisher, IRideQueueStore store) CreateService(
        int maxQueueLength = 500,
        int maxBoardableGroupSize = RideSeatCapacity)
    {
        var options = Options.Create(new QueueModuleOptions
        {
            MaxQueueLength = maxQueueLength,
            MaxBoardableGroupSize = maxBoardableGroupSize,
        });

        var store = new InMemoryRideQueueStore(options);
        var publisher = new Mock<IIntegrationEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new RideQueueService(
            store,
            QueueTestData.Generator(),
            publisher.Object,
            TimeProvider.System,
            NullLogger<RideQueueService>.Instance);

        return (service, publisher, store);
    }

    // --- GroupArrival.PartitionSizes -------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(31)]
    [InlineData(32)]
    public void PartitionSizes_WithinCapacity_KeepsTheGroupWhole(int totalSize)
    {
        var sizes = GroupArrival.PartitionSizes(totalSize, RideSeatCapacity);

        Assert.Equal(totalSize, Assert.Single(sizes));
    }

    [Fact]
    public void PartitionSizes_OversizedGroup_SplitsIntoBoardableGroups()
    {
        var sizes = GroupArrival.PartitionSizes(40, RideSeatCapacity);

        Assert.Equal([20, 20], sizes);
    }

    [Fact]
    public void PartitionSizes_DividesAsEvenlyAsPossible_RatherThanLeavingARemainder()
    {
        var sizes = GroupArrival.PartitionSizes(33, RideSeatCapacity);

        // 17 + 16, never 32 + 1 — a lone straggler group is a worse outcome.
        Assert.Equal([17, 16], sizes);
    }

    [Theory]
    [InlineData(33)]
    [InlineData(40)]
    [InlineData(64)]
    [InlineData(65)]
    [InlineData(100)]
    [InlineData(321)]
    public void PartitionSizes_AlwaysSumsToTheTotal_AndNeverExceedsCapacity(int totalSize)
    {
        var sizes = GroupArrival.PartitionSizes(totalSize, RideSeatCapacity);

        Assert.Equal(totalSize, sizes.Sum());
        Assert.All(sizes, size => Assert.InRange(size, 1, RideSeatCapacity));

        // "Smallest number of groups" — one fewer would have to overrun the capacity.
        Assert.Equal((totalSize + RideSeatCapacity - 1) / RideSeatCapacity, sizes.Count);

        // Evenly sized: no two groups differ by more than one person.
        Assert.True(sizes.Max() - sizes.Min() <= 1);
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(-1, 32)]
    [InlineData(10, 0)]
    [InlineData(10, -5)]
    public void PartitionSizes_WithInvalidArguments_Throws(int totalSize, int maxGroupSize)
    {
        Assert.Throws<DomainValidationException>(() => GroupArrival.PartitionSizes(totalSize, maxGroupSize));
    }

    // --- RideQueue invariants --------------------------------------------------------

    [Fact]
    public void Enqueue_GroupLargerThanTheRideCapacity_IsRejected()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 500, maxBoardableGroupSize: RideSeatCapacity);

        Assert.Throws<DomainValidationException>(() => queue.Enqueue(QueueTestData.Group(33)));
        Assert.Equal(0, queue.GroupCount);
    }

    [Fact]
    public void Enqueue_GroupExactlyAtTheRideCapacity_IsAccepted()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 500, maxBoardableGroupSize: RideSeatCapacity);

        queue.Enqueue(QueueTestData.Group(RideSeatCapacity));

        Assert.Equal(1, queue.GroupCount);
        Assert.Equal(RideSeatCapacity, queue.PeopleWaiting);
    }

    [Fact]
    public void RideQueue_WithNonPositiveBoardableGroupSize_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: 0));
    }

    [Fact]
    public void EnqueueAll_AppendsEveryArrivalAdjacentlyInOrder()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 500, maxBoardableGroupSize: RideSeatCapacity);

        var groups = queue.EnqueueAll([QueueTestData.Group(20), QueueTestData.Group(20)]);

        Assert.Equal(2, groups.Count);
        Assert.Equal(2, queue.GroupCount);
        Assert.Equal(40, queue.PeopleWaiting);

        var waiting = queue.SnapshotGroups();
        Assert.Equal(groups[0].GroupId, waiting[0].GroupId);
        Assert.Equal(groups[1].GroupId, waiting[1].GroupId);
    }

    [Fact]
    public void EnqueueAll_WhenTheBatchWouldOverrunTheQueue_AdmitsNobody()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: RideSeatCapacity);

        Assert.Throws<DomainValidationException>(
            () => queue.EnqueueAll([QueueTestData.Group(6), QueueTestData.Group(6)]));

        // All-or-nothing: the first arrival must not have slipped in on its own.
        Assert.Equal(0, queue.GroupCount);
        Assert.Equal(0, queue.PeopleWaiting);
    }

    [Fact]
    public void EnqueueAll_WithNoArrivals_Throws()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: RideSeatCapacity);

        Assert.Throws<DomainValidationException>(() => queue.EnqueueAll([]));
    }

    // --- RideQueueService ------------------------------------------------------------

    [Fact]
    public async Task EnqueueGroupAsync_PartyWithinCapacity_JoinsAsOneGroup()
    {
        var (service, _, _) = CreateService();

        var groups = await service.EnqueueGroupAsync(Guid.NewGuid(), RideSeatCapacity, CancellationToken.None);

        Assert.Equal(RideSeatCapacity, Assert.Single(groups).Size);
    }

    [Fact]
    public async Task EnqueueGroupAsync_OversizedParty_IsSplitIntoAdjacentBoardableGroups()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();

        var groups = await service.EnqueueGroupAsync(rideId, groupSize: 40, CancellationToken.None);

        Assert.Equal(2, groups.Count);
        Assert.All(groups, group => Assert.Equal(20, group.Size));
        Assert.NotEqual(groups[0].GroupId, groups[1].GroupId);

        // Everybody is in the line, in two adjacent groups.
        var status = service.GetStatus(rideId);
        Assert.Equal(2, status.GroupCount);
        Assert.Equal(40, status.PeopleWaiting);
        Assert.Equal(groups[0].GroupId, status.Groups[0].GroupId);
        Assert.Equal(groups[1].GroupId, status.Groups[1].GroupId);
    }

    [Fact]
    public async Task EnqueueGroupAsync_OversizedParty_PublishesAnEventPerResultingGroup()
    {
        var (service, publisher, _) = CreateService();
        var rideId = Guid.NewGuid();

        var groups = await service.EnqueueGroupAsync(rideId, groupSize: 40, CancellationToken.None);

        foreach (var group in groups)
        {
            publisher.Verify(
                p => p.PublishAsync(
                    It.Is<GroupQueuedIntegrationEvent>(e =>
                        e.RideId == rideId &&
                        e.GroupId == group.GroupId &&
                        e.PeopleCount == group.Size),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        publisher.Verify(
            p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task EnqueueGroupAsync_EveryResultingGroupFitsTheRide()
    {
        var (service, _, _) = CreateService();

        var groups = await service.EnqueueGroupAsync(Guid.NewGuid(), groupSize: 100, CancellationToken.None);

        // A group of N needs ceil(N / 2) of the ride's 16 gondolas.
        Assert.All(groups, group => Assert.True((group.Size + 1) / 2 <= RideSeatCapacity / 2));
        Assert.Equal(100, groups.Sum(g => g.Size));
    }

    [Fact]
    public async Task EnqueueGroupAsync_HonoursAConfiguredBoardableGroupSize()
    {
        var (service, _, _) = CreateService(maxBoardableGroupSize: 6);

        var groups = await service.EnqueueGroupAsync(Guid.NewGuid(), groupSize: 14, CancellationToken.None);

        Assert.Equal(3, groups.Count);
        Assert.All(groups, group => Assert.InRange(group.Size, 1, 6));
        Assert.Equal(14, groups.Sum(g => g.Size));
    }

    [Fact]
    public async Task EnqueueGroupAsync_OversizedParty_WhenTheLineIsTooFull_AdmitsNobody()
    {
        var (service, publisher, _) = CreateService(maxQueueLength: 30);
        var rideId = Guid.NewGuid();

        await Assert.ThrowsAsync<DomainValidationException>(
            () => service.EnqueueGroupAsync(rideId, groupSize: 40, CancellationToken.None));

        var status = service.GetStatus(rideId);
        Assert.Equal(0, status.GroupCount);
        Assert.Equal(0, status.PeopleWaiting);

        publisher.Verify(
            p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
