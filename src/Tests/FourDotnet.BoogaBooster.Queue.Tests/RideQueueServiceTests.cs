using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

public class RideQueueServiceTests
{
    private static (RideQueueService service, Mock<IIntegrationEventPublisher> publisher, IRideQueueStore store) CreateService(int maxQueueLength = 100, TimeProvider? timeProvider = null)
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
    }

    [Fact]
    public async Task TakeGroupAsync_AfterFifteenMinutes_ReportsHalvedHappiness_AndTheRestOfTheMood()
    {
        var clock = new FakeTimeProvider(QueueTestData.QueuedAt);
        var (service, _, store) = CreateService(timeProvider: clock);
        var rideId = Guid.NewGuid();
        var member = QueueTestData.Person(arrivalHappiness: 80, preferredRideIntensity: 90, nausea: 0);
        var group = store.GetOrCreate(rideId).Enqueue(new GroupArrival(Guid.NewGuid(), [member]), clock.GetUtcNow());

        clock.Advance(TimeSpan.FromMinutes(15));
        var taken = await service.TakeGroupAsync(rideId, group.GroupId, CancellationToken.None);

        var person = Assert.Single(taken!.People);
        Assert.Equal(40, person.Happiness, precision: 10);
        Assert.Equal(90, person.PreferredRideIntensity);
        Assert.Equal(0, person.Nausea);
    }

    [Fact]
    public void GetStatus_ReportsEachGuestsCurrentHappiness()
    {
        var clock = new FakeTimeProvider(QueueTestData.QueuedAt);
        var (service, _, store) = CreateService(timeProvider: clock);
        var rideId = Guid.NewGuid();
        store.GetOrCreate(rideId).Enqueue(
            new GroupArrival(Guid.NewGuid(), [QueueTestData.Person(arrivalHappiness: 80)]),
            clock.GetUtcNow());

        clock.Advance(TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59));
        Assert.Equal(80, service.GetStatus(rideId).Groups[0].People[0].Happiness);

        clock.Advance(TimeSpan.FromSeconds(601)); // 15 minutes in total
        Assert.Equal(40, service.GetStatus(rideId).Groups[0].People[0].Happiness, precision: 10);
    }

    [Fact]
    public void GetStatus_AveragesTheHappinessOfEveryoneWaiting()
    {
        var clock = new FakeTimeProvider(QueueTestData.QueuedAt);
        var (service, _, store) = CreateService(timeProvider: clock);
        var rideId = Guid.NewGuid();
        var queue = store.GetOrCreate(rideId);
        queue.Enqueue(
            new GroupArrival(
                Guid.NewGuid(),
                [QueueTestData.Person(1, arrivalHappiness: 60), QueueTestData.Person(2, arrivalHappiness: 70)]),
            clock.GetUtcNow());
        queue.Enqueue(new GroupArrival(Guid.NewGuid(), [QueueTestData.Person(3, arrivalHappiness: 80)]), clock.GetUtcNow());

        var status = service.GetStatus(rideId);

        Assert.Equal(70, status.AverageHappiness);
    }

    [Fact]
    public async Task GetStatus_WithNobodyWaiting_ReportsNoAverageHappiness()
    {
        var (service, _, _) = CreateService();
        var rideId = Guid.NewGuid();

        Assert.Null(service.GetStatus(rideId).AverageHappiness); // the ride has no queue yet

        var group = (await service.EnqueueGroupAsync(rideId, 1, CancellationToken.None)).Single();
        await service.TakeGroupAsync(rideId, group.GroupId, CancellationToken.None);

        Assert.Null(service.GetStatus(rideId).AverageHappiness); // the queue exists but is empty
    }
}
