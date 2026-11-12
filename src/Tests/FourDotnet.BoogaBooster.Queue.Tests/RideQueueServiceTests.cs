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

public class RideQueueServiceTests
{
    private static (RideQueueService service, Mock<IIntegrationEventPublisher> publisher, IRideQueueStore store) CreateService(
        int maxQueueLength = 100,
        int maxSingleRiderQueueLength = 100,
        TimeProvider? timeProvider = null)
    {
        var options = Options.Create(new QueueModuleOptions
        {
            MaxQueueLength = maxQueueLength,
            MaxSingleRiderQueueLength = maxSingleRiderQueueLength,
        });
        var store = new InMemoryRideQueueStore(options);
        var publisher = new Mock<IIntegrationEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<SingleRiderQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new RideQueueService(
            store,
            QueueTestData.Generator(),
            publisher.Object,
            timeProvider ?? TimeProvider.System,
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
        Assert.Equal(0, status.TotalPeopleWaiting);
    }

    // Single riders — status.

    [Fact]
    public async Task GetStatus_ReportsBothLines_WithSinglesAsTheirOwnFigure()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None);
        await service.EnqueueGroupAsync(rideId, 3, CancellationToken.None);
        await service.EnqueueGroupAsync(rideId, 4, CancellationToken.None);
        var first = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        var last = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);

        var status = service.GetStatus(rideId);

        Assert.Equal(3, status.GroupCount);
        Assert.Equal(9, status.PeopleWaiting);
        Assert.Equal(4, status.SingleRidersWaiting);
        Assert.Equal(4, status.SingleRiders.Count);
        Assert.Equal(first, status.SingleRiders[0]);
        Assert.Equal(last, status.SingleRiders[^1]);
        Assert.Equal(13, status.TotalPeopleWaiting);
    }

    [Fact]
    public async Task GetStatus_SingleRidersDoNotInflateTheGroupFigures()
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
        Assert.Equal(before.Groups.Select(g => g.GroupId), after.Groups.Select(g => g.GroupId));
        Assert.Equal(2, after.SingleRidersWaiting);
    }

    [Fact]
    public async Task GetStatus_AGroupOfOne_IsNotReportedAsASingleRider()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();

        await service.EnqueueGroupAsync(rideId, groupSize: 1, CancellationToken.None);

        var status = service.GetStatus(rideId);
        Assert.Equal(1, status.GroupCount);
        Assert.Equal(1, status.PeopleWaiting);
        Assert.Equal(0, status.SingleRidersWaiting);
        Assert.Empty(status.SingleRiders);
    }

    [Fact]
    public async Task GetStatus_WithOnlySingleRiders_ReportsAnEmptyGroupQueue()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();

        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);

        var status = service.GetStatus(rideId);
        Assert.Equal(0, status.GroupCount);
        Assert.Equal(0, status.PeopleWaiting);
        Assert.Empty(status.Groups);
        Assert.Equal(1, status.SingleRidersWaiting);
    }

    // Single riders — enqueue.

    [Fact]
    public async Task EnqueueSingleRiderAsync_PublishesSingleRiderQueuedEvent_NamingTheRideThePersonAndTheMoment()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero));
        var (service, publisher, _) = CreateService(timeProvider: time);
        var rideId = Guid.NewGuid();

        var person = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);

        Assert.Equal("single-rider-queued", TopicNameResolver.Resolve<SingleRiderQueuedIntegrationEvent>());
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<SingleRiderQueuedIntegrationEvent>(e =>
                    e.RideId == rideId &&
                    e.PersonNumber == person.Number &&
                    e.QueuedAt == time.GetUtcNow()),
                It.IsAny<CancellationToken>()),
            Times.Once);
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EnqueueSingleRiderAsync_AddsARealPersonToTheBackOfTheSinglesLine()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();

        var first = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        var second = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);

        Assert.False(string.IsNullOrWhiteSpace(first.Name));
        Assert.InRange(
            first.WeightInKilograms,
            Domain.Person.MinWeightInKilograms,
            Domain.Person.MaxWeightInKilograms);
        Assert.NotEqual(first.Number, second.Number);
        Assert.Equal(
            [first.Number, second.Number],
            store.FindSingles(rideId)!.SnapshotPeople().Select(p => p.Number));
    }

    [Fact]
    public async Task EnqueueSingleRiderAsync_DoesNotJoinTheGroupQueue()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();

        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);

        // A single rider is not a group of one: the regular queue is not even created.
        Assert.Null(store.Find(rideId));
    }

    [Fact]
    public async Task EnqueueSingleRiderAsync_WhenTheSinglesLineIsFull_Throws_PublishesNothing_AndDisplacesNobody()
    {
        var (service, publisher, store) = CreateService(maxSingleRiderQueueLength: 1);
        var rideId = Guid.NewGuid();
        var waiting = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        publisher.Invocations.Clear();

        await Assert.ThrowsAsync<DomainValidationException>(
            () => service.EnqueueSingleRiderAsync(rideId, CancellationToken.None));

        publisher.Verify(
            p => p.PublishAsync(It.IsAny<SingleRiderQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
        var stillWaiting = Assert.Single(store.FindSingles(rideId)!.SnapshotPeople());
        Assert.Equal(waiting.Number, stillWaiting.Number);
    }

    [Fact]
    public async Task EnqueueGroupAsync_StillJoinsTheRegularQueue_WhileTheSinglesLineIsFull()
    {
        var (service, _, store) = CreateService(maxSingleRiderQueueLength: 1);
        var rideId = Guid.NewGuid();
        await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);
        Assert.False(store.FindSingles(rideId)!.CanAccept(1));

        var group = (await service.EnqueueGroupAsync(rideId, groupSize: 4, CancellationToken.None)).Single();

        Assert.Equal(4, group.Size);
        Assert.Equal(4, store.Find(rideId)!.PeopleWaiting);
    }

    // Single riders — take and requeue.

    private static async Task<long[]> EnqueueSingleRidersAsync(RideQueueService service, Guid rideId, int count)
    {
        var numbers = new long[count];
        for (var i = 0; i < count; i++)
        {
            numbers[i] = (await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None)).Number;
        }

        return numbers;
    }

    private static long[] SinglesWaiting(IRideQueueStore store, Guid rideId) =>
        [.. store.FindSingles(rideId)!.SnapshotPeople().Select(p => p.Number)];

    [Fact]
    public async Task TakeSingleRidersAsync_ReturnsAndRemovesTheFrontOfTheLine()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSingleRidersAsync(service, rideId, 5);

        var taken = await service.TakeSingleRidersAsync(rideId, count: 3, CancellationToken.None);

        Assert.Equal(numbers[..3], taken.Select(p => p.Number));
        Assert.Equal(numbers[3..], SinglesWaiting(store, rideId));
    }

    [Fact]
    public async Task TakeSingleRidersAsync_MapsThePeopleItTook()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();
        var enqueued = await service.EnqueueSingleRiderAsync(rideId, CancellationToken.None);

        var taken = Assert.Single(await service.TakeSingleRidersAsync(rideId, count: 1, CancellationToken.None));

        Assert.Equal(enqueued, taken);
    }

    [Fact]
    public async Task TakeSingleRidersAsync_MoreThanWaiting_ReturnsEveryone_AndEmptiesTheLine()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSingleRidersAsync(service, rideId, 2);

        var taken = await service.TakeSingleRidersAsync(rideId, count: 5, CancellationToken.None);

        Assert.Equal(numbers, taken.Select(p => p.Number));
        Assert.Empty(SinglesWaiting(store, rideId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task TakeSingleRidersAsync_ZeroOrFewer_TakesNobody(int count)
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSingleRidersAsync(service, rideId, 2);

        var taken = await service.TakeSingleRidersAsync(rideId, count, CancellationToken.None);

        Assert.Empty(taken);
        Assert.Equal(numbers, SinglesWaiting(store, rideId));
    }

    [Fact]
    public async Task TakeSingleRidersAsync_ForARideWithNoSinglesLine_ReturnsEmpty_WithoutCreatingTheLine()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();

        var taken = await service.TakeSingleRidersAsync(rideId, count: 4, CancellationToken.None);

        Assert.Empty(taken);
        Assert.Null(store.FindSingles(rideId));
    }

    [Fact]
    public async Task TakeSingleRidersAsync_LeavesTheGroupQueueAlone()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();
        var group = (await service.EnqueueGroupAsync(rideId, groupSize: 4, CancellationToken.None)).Single();
        await EnqueueSingleRidersAsync(service, rideId, 2);

        await service.TakeSingleRidersAsync(rideId, count: 2, CancellationToken.None);

        var remaining = Assert.Single(store.Find(rideId)!.SnapshotGroups());
        Assert.Equal(group.GroupId, remaining.GroupId);
        Assert.Equal(4, remaining.Size);
    }

    [Fact]
    public async Task TakeSingleRidersAsync_WhenCancelled_Throws_AndTakesNobody()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSingleRidersAsync(service, rideId, 2);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.TakeSingleRidersAsync(rideId, count: 2, new CancellationToken(canceled: true)));

        Assert.Equal(numbers, SinglesWaiting(store, rideId));
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_RestoresTheFrontOfTheLine_InTheOriginalOrder()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSingleRidersAsync(service, rideId, 3); // A, B, C
        var taken = await service.TakeSingleRidersAsync(rideId, count: 2, CancellationToken.None); // A, B

        await service.RequeueSingleRidersAsync(rideId, taken, CancellationToken.None);

        Assert.Equal(numbers, SinglesWaiting(store, rideId));
        var front = store.FindSingles(rideId)!.SnapshotPeople()[0];
        Assert.Equal(taken[0].Name, front.Name);
        Assert.Equal(taken[0].WeightInKilograms, front.WeightInKilograms);
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_PublishesNoArrivalEvent()
    {
        var (service, publisher, _) = CreateService();
        var rideId = Guid.NewGuid();
        await EnqueueSingleRidersAsync(service, rideId, 2);
        var taken = await service.TakeSingleRidersAsync(rideId, count: 2, CancellationToken.None);
        publisher.Invocations.Clear();

        await service.RequeueSingleRidersAsync(rideId, taken, CancellationToken.None);

        // Returning guests are not new arrivals.
        publisher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_IsNotAbandonedByACancelledToken()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();
        var numbers = await EnqueueSingleRidersAsync(service, rideId, 2);
        var taken = await service.TakeSingleRidersAsync(rideId, count: 2, CancellationToken.None);

        await service.RequeueSingleRidersAsync(rideId, taken, new CancellationToken(canceled: true));

        // The compensation must not lose the guests it exists to protect.
        Assert.Equal(numbers, SinglesWaiting(store, rideId));
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_WithNobody_DoesNotCreateALine()
    {
        var (service, _, store) = CreateService();
        var rideId = Guid.NewGuid();

        await service.RequeueSingleRidersAsync(rideId, [], CancellationToken.None);

        Assert.Null(store.FindSingles(rideId));
    }

    [Fact]
    public async Task RequeueSingleRidersAsync_NullPeople_Throws()
    {
        var (service, _, _) = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => service.RequeueSingleRidersAsync(Guid.NewGuid(), null!, CancellationToken.None));
    }
}
