using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

public class RideQueueServiceTests
{
    private static (RideQueueService service, Mock<IIntegrationEventPublisher> publisher, IRideQueueStore store) CreateService(int maxQueueLength = 100)
    {
        var options = Options.Create(new QueueModuleOptions { MaxQueueLength = maxQueueLength });
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

    [Fact]
    public async Task EnqueueGroupAsync_PublishesGroupQueuedEvent_WithGroupIdAndCount()
    {
        var (service, publisher, _) = CreateService();
        var rideId = Guid.NewGuid();

        var result = (await service.EnqueueGroupAsync(rideId, groupSize: 3, CancellationToken.None)).Single();

        Assert.Equal(3, result.Size);
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<GroupQueuedIntegrationEvent>(e =>
                    e.RideId == rideId &&
                    e.GroupId == result.GroupId &&
                    e.PeopleCount == 3),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueGroupAsync_AddsGroupToRideStatus()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();

        await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None);
        await service.EnqueueGroupAsync(rideId, 4, CancellationToken.None);

        var status = service.GetStatus(rideId);

        Assert.Equal(rideId, status.RideId);
        Assert.Equal(2, status.GroupCount);
        Assert.Equal(6, status.PeopleWaiting);
        Assert.Equal([2, 4], status.Groups.Select(g => g.Size));
    }

    [Fact]
    public async Task EnqueueGroupAsync_PopulatesGroupWithRealPeople()
    {
        var (service, _, _) = CreateService();

        var result = (await service.EnqueueGroupAsync(Guid.NewGuid(), groupSize: 4, CancellationToken.None)).Single();

        Assert.Equal(4, result.People.Count);
        Assert.Equal(4, result.People.Select(p => p.Number).Distinct().Count());
        Assert.All(result.People, p => Assert.False(string.IsNullOrWhiteSpace(p.Name)));
        Assert.All(
            result.People,
            p => Assert.InRange(
                p.WeightInKilograms,
                Domain.Person.MinWeightInKilograms,
                Domain.Person.MaxWeightInKilograms));
        Assert.Equal(result.People.Sum(p => p.WeightInKilograms), result.TotalWeightInKilograms);
    }

    [Fact]
    public async Task EnqueueGroupAsync_RejectsNonPositiveSize()
    {
        var (service, publisher, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.EnqueueGroupAsync(Guid.NewGuid(), 0, CancellationToken.None));

        publisher.Verify(
            p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TakeGroupAsync_RemovesTheChosenGroup_AndReturnsIt()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None);
        var second = (await service.EnqueueGroupAsync(rideId, 4, CancellationToken.None)).Single();

        var taken = await service.TakeGroupAsync(rideId, second.GroupId, CancellationToken.None);

        Assert.NotNull(taken);
        Assert.Equal(second.GroupId, taken!.GroupId);
        Assert.Equal(4, taken.Size);

        var status = service.GetStatus(rideId);
        Assert.Equal(1, status.GroupCount);
        Assert.Equal(2, status.PeopleWaiting);
        Assert.DoesNotContain(status.Groups, g => g.GroupId == second.GroupId);
    }

    [Fact]
    public async Task TakeGroupAsync_ForAbsentOrAlreadyTakenGroup_ReturnsNull()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();
        var group = (await service.EnqueueGroupAsync(rideId, 3, CancellationToken.None)).Single();

        Assert.Null(await service.TakeGroupAsync(rideId, Guid.NewGuid(), CancellationToken.None));

        Assert.NotNull(await service.TakeGroupAsync(rideId, group.GroupId, CancellationToken.None));
        Assert.Null(await service.TakeGroupAsync(rideId, group.GroupId, CancellationToken.None));
    }

    [Fact]
    public async Task TakeGroupAsync_ForUnknownRide_ReturnsNull()
    {
        var (service, _, _) = CreateService();

        var taken = await service.TakeGroupAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.Null(taken);
    }

    [Fact]
    public void GetStatus_ForUnknownRide_ReturnsEmptySnapshot()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();

        var status = service.GetStatus(rideId);

        Assert.Equal(rideId, status.RideId);
        Assert.Equal(0, status.GroupCount);
        Assert.Equal(0, status.PeopleWaiting);
        Assert.Empty(status.Groups);
        Assert.Equal(0, status.SingleRidersWaiting);
        Assert.Empty(status.SingleRiders);
    }

    [Fact]
    public async Task EnqueueSingleRiderAsync_PublishesSingleRiderQueuedEvent()
    {
        var (service, publisher, _) = CreateService();
        var rideId = Guid.NewGuid();

        var person = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<SingleRiderQueuedIntegrationEvent>(e =>
                    e.RideId == rideId && e.PersonNumber == person.Number),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatus_ReportsBothLines_WithGroupFiguresUnaffectedBySingles()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, 4, CancellationToken.None);
        var before = service.GetStatus(rideId);

        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        var after = service.GetStatus(rideId);

        Assert.Equal(before.GroupCount, after.GroupCount);
        Assert.Equal(before.PeopleWaiting, after.PeopleWaiting);
        Assert.Equal(4, after.PeopleWaiting);
        Assert.Equal(2, after.SingleRidersWaiting);
        Assert.Equal(2, after.SingleRiders.Count);
        Assert.Equal(6, after.TotalPeopleWaiting);
    }

    [Fact]
    public async Task GetStatus_ForAGroupOfOne_DoesNotListItAsASingleRider()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();

        await service.EnqueueGroupAsync(rideId, 1, CancellationToken.None);

        var status = service.GetStatus(rideId);
        Assert.Equal(1, status.GroupCount);
        Assert.Equal(0, status.SingleRidersWaiting);
    }

    [Fact]
    public async Task TakeSingleRidersAsync_ReturnsAndRemovesFromTheFront()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();
        var first = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        var second = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        var third = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);

        var taken = await service.TakeSingleRidersAsync(rideId, 2, CancellationToken.None);

        Assert.Equal([first.Number, second.Number], taken.Select(p => p.Number));
        var remaining = Assert.Single(service.GetStatus(rideId).SingleRiders);
        Assert.Equal(third.Number, remaining.Number);
    }

    [Fact]
    public async Task TakeSingleRidersAsync_WithoutALine_ReturnsEmpty()
    {
        var (service, _, _) = CreateService();

        var taken = await service.TakeSingleRidersAsync(Guid.NewGuid(), 3, CancellationToken.None);

        Assert.Empty(taken);
    }

    [Fact]
    public async Task TakeSingleRidersAsync_FromAnEmptiedLine_ReturnsEmpty()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();
        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        await service.TakeSingleRidersAsync(rideId, 5, CancellationToken.None);

        Assert.Empty(await service.TakeSingleRidersAsync(rideId, 5, CancellationToken.None));
        Assert.Empty(await service.TakeSingleRidersAsync(rideId, 0, CancellationToken.None));
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_RestoresTheFrontOfTheLine()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();
        var a = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        var b = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        var c = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        var taken = await service.TakeSingleRidersAsync(rideId, 2, CancellationToken.None);

        await service.RequeueSingleRidersAsync(rideId, taken, CancellationToken.None);

        Assert.Equal(
            [a.Number, b.Number, c.Number],
            service.GetStatus(rideId).SingleRiders.Select(p => p.Number));
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_WithNobody_IsANoOp()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();

        await service.RequeueSingleRidersAsync(rideId, [], CancellationToken.None);

        Assert.Null(store.FindSingles(rideId));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RequeueSingleRidersAsync(rideId, null!, CancellationToken.None));
    }

    [Fact]
    public async Task SingleRiderMethods_HonourCancellation()
    {
        var (service, _, _) = CreateService();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.TakeSingleRidersAsync(Guid.NewGuid(), 1, cts.Token));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.RequeueSingleRidersAsync(Guid.NewGuid(), [new Abstractions.DataTransferObjects.PersonDto(1, "A", 80)], cts.Token));
    }
}
