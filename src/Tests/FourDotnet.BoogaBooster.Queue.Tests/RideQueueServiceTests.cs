using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Filling;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

public class RideQueueServiceTests
{
    private const int Precision = 10;

    private static (RideQueueService service, Mock<IIntegrationEventPublisher> publisher, IRideQueueStore store) CreateService(
        int maxQueueLength = 100,
        TimeProvider? timeProvider = null,
        IPersonGenerator? personGenerator = null)
    {
        var options = Options.Create(new QueueModuleOptions { MaxQueueLength = maxQueueLength });
        var store = new InMemoryRideQueueStore(options);
        var publisher = new Mock<IIntegrationEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = new RideQueueService(
            store,
            personGenerator ?? QueueTestData.Generator(),
            publisher.Object,
            timeProvider ?? TimeProvider.System,
            NullLogger<RideQueueService>.Instance);

        return (service, publisher, store);
    }

    /// <summary>
    /// A generator that hands out one scripted group per requested size, so a test can
    /// control the arrival happiness of the people the service enqueues.
    /// </summary>
    private static IPersonGenerator GeneratorProducing(params double[] happiness)
    {
        var generator = new Mock<IPersonGenerator>();
        generator
            .Setup(g => g.CreateGroup(happiness.Length))
            .Returns(() => QueueTestData.GroupWithHappiness(happiness));

        return generator.Object;
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
        Assert.Null(status.AverageHappiness);
    }

    [Fact]
    public async Task EnqueueGroupAsync_StampsTheEventAndTheGroup_WithTheClockReading()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var (service, publisher, store) = CreateService(timeProvider: time);
        var rideId = Guid.NewGuid();

        var group = (await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None)).Single();

        Assert.Equal(QueueTestData.Now, store.Find(rideId)!.SnapshotGroups().Single().QueuedAt);
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<GroupQueuedIntegrationEvent>(e => e.GroupId == group.GroupId && e.QueuedAt == QueueTestData.Now),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task EnqueueGroupAsync_ReturnsEachPersonsProfile_WithArrivalHappiness()
    {
        var (service, _, _) = CreateService(
            timeProvider: new FakeTimeProvider(QueueTestData.Now),
            personGenerator: GeneratorProducing(0.6, 0.8));

        var group = (await service.EnqueueGroupAsync(Guid.NewGuid(), 2, CancellationToken.None)).Single();

        // Nobody has waited yet, so the reported happiness is the arrival value.
        Assert.Equal([0.6, 0.8], group.People.Select(p => p.Happiness));
        Assert.All(group.People, p => Assert.Equal(0.5, p.PreferredIntensity));
        Assert.All(group.People, p => Assert.Equal(0, p.Nausea));
    }

    [Fact]
    public async Task GetStatus_CarriesEveryPersonsProfile()
    {
        var (service, _, _) = CreateService(timeProvider: new FakeTimeProvider(QueueTestData.Now));
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, 3, CancellationToken.None);

        var status = service.GetStatus(rideId);

        var people = status.Groups.Single().People;
        Assert.Equal(3, people.Count);
        Assert.All(
            people,
            p => Assert.InRange(
                p.PreferredIntensity,
                Domain.RiderProfile.MinPreferredIntensity,
                Domain.RiderProfile.MaxPreferredIntensity));
        Assert.All(
            people,
            p => Assert.InRange(p.Happiness, PersonGenerator.MinArrivalHappiness, PersonGenerator.MaxArrivalHappiness));
        Assert.All(people, p => Assert.Equal(0, p.Nausea));
    }

    [Fact]
    public async Task GetStatus_ReportsTheAverageOfCurrentHappiness()
    {
        var (service, _, _) = CreateService(
            timeProvider: new FakeTimeProvider(QueueTestData.Now),
            personGenerator: GeneratorProducing(0.6, 0.8));
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None);

        var status = service.GetStatus(rideId);

        Assert.NotNull(status.AverageHappiness);
        Assert.Equal(0.7, status.AverageHappiness.Value, Precision);
    }

    [Fact]
    public async Task GetStatus_AfterTheLineEmpties_HasNoAverage()
    {
        var (service, _, _) = CreateService(timeProvider: new FakeTimeProvider(QueueTestData.Now));
        var rideId = Guid.NewGuid();
        var group = (await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None)).Single();

        await service.TakeGroupAsync(rideId, group.GroupId, CancellationToken.None);

        var status = service.GetStatus(rideId);
        Assert.Equal(0, status.PeopleWaiting);
        Assert.Null(status.AverageHappiness);
    }

    [Fact]
    public async Task GetStatus_AfterALongWait_ReportsWaitAdjustedHappiness_AndAverage()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var (service, _, store) = CreateService(
            timeProvider: time,
            personGenerator: GeneratorProducing(0.8, 0.6));
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None);

        time.Advance(TimeSpan.FromMinutes(15));
        var status = service.GetStatus(rideId);

        // Ten minutes past the five-minute onset at 0.01/min costs everyone 0.1.
        var people = status.Groups.Single().People;
        Assert.Equal(0.7, people[0].Happiness, Precision);
        Assert.Equal(0.5, people[1].Happiness, Precision);
        Assert.Equal(0.6, status.AverageHappiness!.Value, Precision);

        // The stored profiles are untouched: only the reported value moved.
        var stored = store.Find(rideId)!.SnapshotGroups().Single().Members;
        Assert.Equal([0.8, 0.6], stored.Select(m => m.Profile.Happiness));
    }

    [Fact]
    public async Task TakeGroupAsync_ReturnsWaitAdjustedHappiness_AsOfTheTake()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var (service, _, _) = CreateService(
            timeProvider: time,
            personGenerator: GeneratorProducing(0.8, 0.7));
        var rideId = Guid.NewGuid();
        var enqueued = (await service.EnqueueGroupAsync(rideId, 2, CancellationToken.None)).Single();

        time.Advance(TimeSpan.FromMinutes(15));
        var taken = await service.TakeGroupAsync(rideId, enqueued.GroupId, CancellationToken.None);

        Assert.NotNull(taken);
        Assert.Equal(0.7, taken!.People[0].Happiness, Precision);
        Assert.Equal(0.6, taken.People[1].Happiness, Precision);
        Assert.All(taken.People, p => Assert.Equal(0.5, p.PreferredIntensity));
        Assert.All(taken.People, p => Assert.Equal(0, p.Nausea));
    }
}
