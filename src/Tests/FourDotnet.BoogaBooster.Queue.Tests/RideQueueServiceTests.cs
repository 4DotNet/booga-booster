using FourDotnet.BoogaBooster.Core;
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
    private static (RideQueueService service, Mock<IIntegrationEventPublisher> publisher, IRideQueueStore store) CreateService(
        int maxQueueLength = 100,
        TimeProvider? timeProvider = null)
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
            options,
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
    public async Task EnqueueGroupAsync_StampsTheGroupWithTheTimeProvidersNow()
    {
        var time = new FakeTimeProvider(QueueTestData.QueuedAt);
        var (service, publisher, store) = CreateService(timeProvider: time);
        var rideId = Guid.NewGuid();
        time.Advance(TimeSpan.FromMinutes(3));

        var group = (await service.EnqueueGroupAsync(rideId, groupSize: 2, CancellationToken.None)).Single();

        var expected = QueueTestData.QueuedAt.AddMinutes(3);
        Assert.Equal(expected, store.Find(rideId)!.SnapshotGroups().Single().QueuedAt);
        publisher.Verify(
            p => p.PublishAsync(
                It.Is<GroupQueuedIntegrationEvent>(e => e.GroupId == group.GroupId && e.QueuedAt == expected),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TakeGroupAsync_AfterFifteenMinutes_CarriesTheDecayedHappiness()
    {
        var time = new FakeTimeProvider(QueueTestData.QueuedAt);
        var (service, _, store) = CreateService(timeProvider: time);
        var rideId = Guid.NewGuid();
        var arrival = new GroupArrival(
            Guid.NewGuid(),
            [QueueTestData.Person(number: 1, happiness: 80, preferredRideIntensity: 90, nausea: 3)]);
        store.GetOrCreate(rideId).Enqueue(arrival, time.GetUtcNow());
        time.Advance(TimeSpan.FromMinutes(15));

        var taken = await service.TakeGroupAsync(rideId, arrival.GroupId, CancellationToken.None);

        var person = Assert.Single(taken!.People);
        Assert.Equal(40d, person.Happiness, 1e-9);
        Assert.Equal(90d, person.PreferredRideIntensity);
        Assert.Equal(3d, person.Nausea);
    }

    [Theory]
    [InlineData(4, 80)]
    [InlineData(15, 40)]
    [InlineData(25, 20)]
    public void GetStatus_ReportsEachGuestsCurrentHappiness(int minutesWaited, double expected)
    {
        var time = new FakeTimeProvider(QueueTestData.QueuedAt);
        var (service, _, store) = CreateService(timeProvider: time);
        var rideId = Guid.NewGuid();
        store.GetOrCreate(rideId).Enqueue(
            new GroupArrival(Guid.NewGuid(), [QueueTestData.Person(happiness: 80, preferredRideIntensity: 60)]),
            time.GetUtcNow());
        time.Advance(TimeSpan.FromMinutes(minutesWaited));

        var person = service.GetStatus(rideId).Groups.Single().People.Single();

        Assert.Equal(expected, person.Happiness, 1e-9);
        Assert.Equal(60d, person.PreferredRideIntensity);
        Assert.Equal(0d, person.Nausea);
    }

    [Fact]
    public async Task EnqueueGroupAsync_ReturnsGuestsWithTheirArrivalExperience()
    {
        var (service, _, _) = CreateService();

        var group = (await service.EnqueueGroupAsync(Guid.NewGuid(), groupSize: 5, CancellationToken.None)).Single();

        Assert.All(group.People, p => Assert.InRange(p.Happiness, 65d, 85d));
        Assert.All(group.People, p => Assert.InRange(p.PreferredRideIntensity, 50d, 100d));
        Assert.All(group.People, p => Assert.Equal(0d, p.Nausea));
    }

    [Fact]
    public void Constructor_WithAnInvalidHappinessHalfLife_Throws()
    {
        var options = Options.Create(new QueueModuleOptions { HappinessHalfLife = TimeSpan.Zero });

        Assert.Throws<DomainValidationException>(() => new RideQueueService(
            new InMemoryRideQueueStore(options),
            QueueTestData.Generator(),
            new Mock<IIntegrationEventPublisher>().Object,
            TimeProvider.System,
            options,
            NullLogger<RideQueueService>.Instance));
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
}
