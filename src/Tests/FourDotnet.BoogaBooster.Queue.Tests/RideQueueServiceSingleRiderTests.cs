using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// The single-riders half of <see cref="RideQueueService"/>: enqueuing, taking in
/// bulk, requeueing to the front and reporting the line beside the groups.
/// </summary>
public sealed class RideQueueServiceSingleRiderTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    private static (RideQueueService Service, Mock<IIntegrationEventPublisher> Publisher, IRideQueueStore Store) Create(
        int maxSingleRiderQueueLength = 10)
    {
        var options = Options.Create(new QueueModuleOptions { MaxSingleRiderQueueLength = maxSingleRiderQueueLength });
        var store = new InMemoryRideQueueStore(options);
        var publisher = new Mock<IIntegrationEventPublisher>();

        var service = new RideQueueService(
            store,
            QueueTestData.Generator(),
            publisher.Object,
            new FakeTimeProvider(Now),
            NullLogger<RideQueueService>.Instance);

        return (service, publisher, store);
    }

    private static async Task<long[]> EnqueueSinglesAsync(RideQueueService service, Guid rideId, int count)
    {
        var numbers = new long[count];
        for (var i = 0; i < count; i++)
        {
            numbers[i] = (await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None))!.Number;
        }

        return numbers;
    }

    [Fact]
    public async Task EnqueueSingleRiderAsync_PublishesSingleRiderQueued_ForThePerson()
    {
        var (service, publisher, _) = Create();
        var rideId = Guid.NewGuid();

        var person = (await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None))!;

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<SingleRiderQueuedIntegrationEvent>(e =>
                    e.RideId == rideId &&
                    e.PersonNumber == person.Number &&
                    e.QueuedAt == Now),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueSingleRiderAsync_AppendsToTheSinglesLine_InArrivalOrder()
    {
        var (service, _, _) = Create();
        var rideId = Guid.NewGuid();

        var numbers = await EnqueueSinglesAsync(service, rideId, 3);

        var status = service.GetStatus(rideId);
        Assert.Equal(3, status.SingleRidersWaiting);
        Assert.Equal(numbers, status.SingleRiders.Select(p => p.Number));
    }

    [Fact]
    public async Task EnqueueSingleRiderAsync_WhenTheLineIsFull_ReturnsNullAndPublishesNothing()
    {
        var (service, publisher, _) = Create(maxSingleRiderQueueLength: 1);
        var rideId = Guid.NewGuid();
        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        publisher.Invocations.Clear();

        Assert.Null(await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None));

        Assert.Equal(1, service.GetStatus(rideId).SingleRidersWaiting);
        Assert.Empty(publisher.Invocations);
    }

    [Fact]
    public async Task EnqueueSingleRiderAsync_DoesNotTouchTheGroupQueue()
    {
        var (service, _, store) = Create();
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, 4, CancellationToken.None);

        await EnqueueSinglesAsync(service, rideId, 2);

        Assert.Equal(1, store.Find(rideId)!.GroupCount);
        Assert.Equal(4, store.Find(rideId)!.PeopleWaiting);
        Assert.Equal(2, store.FindSingles(rideId)!.PeopleWaiting);
    }

    [Fact]
    public async Task TakeSingleRidersAsync_ReturnsAndRemovesFromTheFront()
    {
        var (service, _, _) = Create();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSinglesAsync(service, rideId, 5);

        var taken = await service.TakeSingleRidersAsync(rideId, 3, CancellationToken.None);

        Assert.Equal(numbers[..3], taken.Select(p => p.Number));
        Assert.Equal(numbers[3..], service.GetStatus(rideId).SingleRiders.Select(p => p.Number));
    }

    [Fact]
    public async Task TakeSingleRidersAsync_MoreThanWaiting_ReturnsEveryone()
    {
        var (service, _, _) = Create();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSinglesAsync(service, rideId, 2);

        var taken = await service.TakeSingleRidersAsync(rideId, 5, CancellationToken.None);

        Assert.Equal(numbers, taken.Select(p => p.Number));
        Assert.Equal(0, service.GetStatus(rideId).SingleRidersWaiting);
    }

    [Fact]
    public async Task TakeSingleRidersAsync_FromAnEmptyLine_ReturnsEmpty()
    {
        var (service, _, store) = Create();
        var rideId = Guid.NewGuid();
        store.GetOrCreateSingles(rideId);

        Assert.Empty(await service.TakeSingleRidersAsync(rideId, 3, CancellationToken.None));
    }

    [Fact]
    public async Task TakeSingleRidersAsync_ForARideWithNoSinglesLine_ReturnsEmpty()
    {
        var (service, _, store) = Create();
        var rideId = Guid.NewGuid();

        var taken = await service.TakeSingleRidersAsync(rideId, 3, CancellationToken.None);

        Assert.Empty(taken);
        Assert.Null(store.FindSingles(rideId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TakeSingleRidersAsync_NonPositiveCount_TakesNobody(int count)
    {
        var (service, _, _) = Create();
        var rideId = Guid.NewGuid();
        await EnqueueSinglesAsync(service, rideId, 2);

        var taken = await service.TakeSingleRidersAsync(rideId, count, CancellationToken.None);

        Assert.Empty(taken);
        Assert.Equal(2, service.GetStatus(rideId).SingleRidersWaiting);
    }

    [Fact]
    public async Task TakeSingleRidersAsync_WhenCancelled_Throws()
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.TakeSingleRidersAsync(Guid.NewGuid(), 1, new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_RestoresTheFrontOfTheLine_InOriginalOrder()
    {
        var (service, _, _) = Create();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSinglesAsync(service, rideId, 3);
        var taken = await service.TakeSingleRidersAsync(rideId, 2, CancellationToken.None);

        await service.RequeueSingleRidersAsync(rideId, taken, CancellationToken.None);

        var status = service.GetStatus(rideId);
        Assert.Equal(numbers, status.SingleRiders.Select(p => p.Number));
        Assert.Equal(taken, status.SingleRiders.Take(2));
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_WithNobody_LeavesTheLineAlone()
    {
        var (service, _, store) = Create();
        var rideId = Guid.NewGuid();

        await service.RequeueSingleRidersAsync(rideId, [], CancellationToken.None);

        Assert.Null(store.FindSingles(rideId));
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_Null_Throws()
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RequeueSingleRidersAsync(Guid.NewGuid(), null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetStatus_ReportsBothLines_WithGroupCountsUnaffectedBySingles()
    {
        var (service, _, _) = Create();
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None);
        await service.EnqueueGroupAsync(rideId, 3, CancellationToken.None);
        await service.EnqueueGroupAsync(rideId, 4, CancellationToken.None);
        var before = service.GetStatus(rideId);

        await EnqueueSinglesAsync(service, rideId, 4);

        var status = service.GetStatus(rideId);
        Assert.Equal(3, status.GroupCount);
        Assert.Equal(9, status.PeopleWaiting);
        Assert.Equal(4, status.SingleRidersWaiting);
        Assert.Equal(4, status.SingleRiders.Count);
        Assert.Equal(13, status.TotalPeopleWaiting);
        Assert.Equal(before.GroupCount, status.GroupCount);
        Assert.Equal(before.PeopleWaiting, status.PeopleWaiting);
    }

    [Fact]
    public async Task GetStatus_AGroupOfOne_IsNotASingleRider()
    {
        var (service, _, _) = Create();
        var rideId = Guid.NewGuid();

        await service.EnqueueGroupAsync(rideId, 1, CancellationToken.None);

        var status = service.GetStatus(rideId);
        Assert.Equal(1, status.GroupCount);
        Assert.Equal(0, status.SingleRidersWaiting);
        Assert.Empty(status.SingleRiders);
    }

    [Fact]
    public void GetStatus_ForAnUnusedRide_ReportsBothLinesEmpty()
    {
        var (service, _, _) = Create();

        var status = service.GetStatus(Guid.NewGuid());

        Assert.Equal(0, status.PeopleWaiting);
        Assert.Equal(0, status.SingleRidersWaiting);
        Assert.Empty(status.SingleRiders);
        Assert.Equal(0, status.TotalPeopleWaiting);
    }
}
