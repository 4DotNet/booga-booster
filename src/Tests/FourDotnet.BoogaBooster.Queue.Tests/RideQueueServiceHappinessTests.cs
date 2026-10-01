using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers how <see cref="RideQueueService"/> reports guest experience: it stamps each
/// group's join time from the injected <see cref="TimeProvider"/>, and every status
/// read and every take reports happiness eroded by the time waited so far
/// (docs/06-passenger-experience.md §6.2), driven here by a <see cref="FakeTimeProvider"/>.
/// </summary>
public sealed class RideQueueServiceHappinessTests
{
    private static readonly DateTimeOffset Noon = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Noon);
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();
    private readonly InMemoryRideQueueStore _store;
    private readonly RideQueueService _service;
    private readonly Guid _rideId = Guid.NewGuid();

    public RideQueueServiceHappinessTests()
    {
        _store = new InMemoryRideQueueStore(Options.Create(new QueueModuleOptions { MaxQueueLength = 100 }));
        _publisher
            .Setup(p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _service = new RideQueueService(
            _store,
            QueueTestData.Generator(),
            _publisher.Object,
            _clock,
            NullLogger<RideQueueService>.Instance);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EnqueueGroupAsync_StampsTheJoinTimeFromTheTimeProvider()
    {
        var group = (await _service.EnqueueGroupAsync(_rideId, groupSize: 2, Ct)).Single();

        var queued = Assert.Single(_store.Find(_rideId)!.SnapshotGroups());
        Assert.Equal(Noon, queued.QueuedAt);
        _publisher.Verify(
            p => p.PublishAsync(
                It.Is<GroupQueuedIntegrationEvent>(e => e.GroupId == group.GroupId && e.QueuedAt == Noon),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetStatus_AfterTenMinutes_ReportsHappinessErodedByFiveMinutesPastTheGrace()
    {
        await _service.EnqueueGroupAsync(_rideId, groupSize: 3, Ct);
        var atArrival = _service.GetStatus(_rideId);
        var arrivalHappiness = atArrival.Groups.Single().People.Select(p => p.Happiness).ToArray();

        _clock.Advance(TimeSpan.FromMinutes(10));
        var later = _service.GetStatus(_rideId);

        // Ten minutes waited is five past the grace period: half a time constant.
        var factor = Math.Exp(-0.5d);
        var people = later.Groups.Single().People;
        for (var i = 0; i < people.Count; i++)
        {
            Assert.Equal(arrivalHappiness[i] * factor, people[i].Happiness, precision: 10);
        }

        Assert.NotNull(later.AverageHappiness);
        Assert.Equal(arrivalHappiness.Average() * factor, later.AverageHappiness!.Value, precision: 10);
    }

    [Fact]
    public void GetStatus_ReportsEachPersonsRatings_AndTheAverageHappiness()
    {
        EnqueueKnownGroup(
            QueueTestData.Person(number: 1, happiness: 70d, preferredIntensity: 55d, nausea: 0d),
            QueueTestData.Person(number: 2, happiness: 80d, preferredIntensity: 95d, nausea: 0d));

        var status = _service.GetStatus(_rideId);

        var people = status.Groups.Single().People;
        Assert.Equal(
            [new PersonDto(1, "Test Person", 80, 70d, 55d, 0d), new PersonDto(2, "Test Person", 80, 80d, 95d, 0d)],
            people);
        Assert.Equal(75d, status.AverageHappiness);
    }

    [Fact]
    public void GetStatus_AveragesOverEveryPerson_NotOverGroups()
    {
        EnqueueKnownGroup(QueueTestData.Person(number: 1, happiness: 60d));
        EnqueueKnownGroup(
            QueueTestData.Person(number: 2, happiness: 80d),
            QueueTestData.Person(number: 3, happiness: 85d));

        var status = _service.GetStatus(_rideId);

        Assert.Equal(75d, status.AverageHappiness!.Value, precision: 10);
    }

    [Fact]
    public void GetStatus_ReadThreeMinutesApartPastTheGrace_ReportsLowerHappinessAndAverage()
    {
        EnqueueKnownGroup(QueueTestData.Person(number: 1, happiness: 80d), QueueTestData.Person(number: 2, happiness: 70d));
        _clock.Advance(TimeSpan.FromMinutes(6));

        var first = _service.GetStatus(_rideId);
        _clock.Advance(TimeSpan.FromMinutes(3));
        var second = _service.GetStatus(_rideId);

        var before = first.Groups.Single().People;
        var after = second.Groups.Single().People;
        Assert.All(Enumerable.Range(0, before.Count), i => Assert.True(after[i].Happiness < before[i].Happiness));
        Assert.True(second.AverageHappiness < first.AverageHappiness);
    }

    [Fact]
    public void GetStatus_ForAnUnknownRide_ReportsNoAverage()
    {
        var status = _service.GetStatus(Guid.NewGuid());

        Assert.Null(status.AverageHappiness);
    }

    [Fact]
    public async Task GetStatus_ForAQueueThatHasEmptied_ReportsNoAverage()
    {
        var group = (await _service.EnqueueGroupAsync(_rideId, groupSize: 2, Ct)).Single();
        await _service.TakeGroupAsync(_rideId, group.GroupId, Ct);

        var status = _service.GetStatus(_rideId);

        Assert.Equal(0, status.PeopleWaiting);
        Assert.Null(status.AverageHappiness);
    }

    [Fact]
    public async Task TakeGroupAsync_ReportsTheHappinessErodedUpToTheMomentOfTaking()
    {
        var group = EnqueueKnownGroup(QueueTestData.Person(number: 1, happiness: 80d, preferredIntensity: 90d));
        _clock.Advance(TimeSpan.FromMinutes(15));

        var taken = await _service.TakeGroupAsync(_rideId, group.GroupId, Ct);

        var person = Assert.Single(taken!.People);
        Assert.Equal(80d / Math.E, person.Happiness, precision: 10);
        Assert.Equal(90d, person.PreferredIntensity);
        Assert.Equal(0d, person.Nausea);
    }

    [Fact]
    public async Task TakeGroupAsync_WithinTheGracePeriod_CarriesTheArrivalHappiness()
    {
        var group = EnqueueKnownGroup(QueueTestData.Person(number: 1, happiness: 80d));
        _clock.Advance(TimeSpan.FromMinutes(4));

        var taken = await _service.TakeGroupAsync(_rideId, group.GroupId, Ct);

        Assert.Equal(80d, Assert.Single(taken!.People).Happiness);
    }

    /// <summary>Puts a group of known people straight into the ride's queue, joined at the clock's current time.</summary>
    private QueuedGroup EnqueueKnownGroup(params Person[] members) =>
        _store.GetOrCreate(_rideId).Enqueue(QueueTestData.Group(members), _clock.GetUtcNow());
}
