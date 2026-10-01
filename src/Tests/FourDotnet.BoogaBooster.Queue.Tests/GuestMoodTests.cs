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

/// <summary>
/// Covers the queue half of the passenger-mood capability: a guest's initial mood,
/// its validation, the queue-wait happiness decay, and the decayed happiness the
/// queue reports on every read.
/// </summary>
public sealed class GuestMoodTests
{
    private const double Precision = 1e-9;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // --- Initial mood ---

    [Fact]
    public void PersonGenerator_DrawsInitialMoodInRange()
    {
        var generator = QueueTestData.Generator();

        for (var i = 0; i < 500; i++)
        {
            var person = generator.Next();

            Assert.InRange(person.Happiness, 65d, 85d);
            Assert.InRange(person.PreferredG, 2.25d, 4.5d);
            Assert.Equal(0d, person.Nausea);
        }
    }

    [Fact]
    public void PersonGenerator_WithTheSameSeed_ProducesTheSameMoods()
    {
        var first = QueueTestData.Generator(seed: 42);
        var second = QueueTestData.Generator(seed: 42);

        var a = Enumerable.Range(0, 50).Select(_ => first.Next()).Select(p => (p.Happiness, p.PreferredG)).ToArray();
        var b = Enumerable.Range(0, 50).Select(_ => second.Next()).Select(p => (p.Happiness, p.PreferredG)).ToArray();

        Assert.Equal(a, b);
    }

    [Fact]
    public void PreferredGRange_IsHalfToFullSafeLimit()
    {
        Assert.Equal(4.5d, Person.MaxPreferredG);
        Assert.Equal(2.25d, Person.MinPreferredG);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public void Person_WithHappinessOutOfRange_Throws(double happiness)
    {
        Assert.Throws<DomainValidationException>(() => QueueTestData.Person(happiness: happiness));
    }

    [Theory]
    [InlineData(2.2)]
    [InlineData(4.6)]
    [InlineData(double.PositiveInfinity)]
    public void Person_WithPreferredGOutOfRange_Throws(double preferredG)
    {
        Assert.Throws<DomainValidationException>(() => QueueTestData.Person(preferredG: preferredG));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Person_WithNauseaOutOfRange_Throws(double nausea)
    {
        Assert.Throws<DomainValidationException>(() => QueueTestData.Person(nausea: nausea));
    }

    [Theory]
    [InlineData(0, 2.25, 0)]
    [InlineData(100, 4.5, 100)]
    public void Person_AtTheBounds_IsValid(double happiness, double preferredG, double nausea)
    {
        var person = QueueTestData.Person(happiness: happiness, preferredG: preferredG, nausea: nausea);

        Assert.Equal(happiness, person.Happiness);
        Assert.Equal(preferredG, person.PreferredG);
        Assert.Equal(nausea, person.Nausea);
    }

    // --- Queue-wait decay ---

    [Fact]
    public void Decay_WithinFiveMinutes_LeavesHappinessUnchanged()
    {
        Assert.Equal(80d, QueueWaitDecay.Apply(80d, TimeSpan.FromMinutes(4)));
        Assert.Equal(80d, QueueWaitDecay.Apply(80d, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Decay_AfterTenMinutes_LosesFiveTimesEMinusOne()
    {
        var happiness = QueueWaitDecay.Apply(80d, TimeSpan.FromMinutes(10));

        Assert.Equal(80d - (5d * (Math.E - 1d)), happiness, Precision);
        Assert.Equal(71.4d, happiness, 0.05d);
    }

    [Fact]
    public void Decay_AfterSixteenMinutes_MakesALowStarterMad()
    {
        var happiness = QueueWaitDecay.Apply(65d, TimeSpan.FromMinutes(16));

        Assert.Equal(65d - (5d * (Math.Exp(2.2d) - 1d)), happiness, Precision);
        Assert.Equal(24.9d, happiness, 0.05d);
        Assert.True(happiness < 30d);
    }

    [Fact]
    public void Decay_BeyondTheStartingHappiness_ClampsAtZero()
    {
        Assert.Equal(0d, QueueWaitDecay.Apply(65d, TimeSpan.FromMinutes(60)));
    }

    // --- Decay applied on read ---

    [Fact]
    public async Task EnqueueGroupAsync_RecordsTheEnqueueTime()
    {
        var (service, store, time) = CreateService();
        var rideId = Guid.NewGuid();

        await service.EnqueueGroupAsync(rideId, groupSize: 2, Ct);

        var group = Assert.Single(store.Find(rideId)!.SnapshotGroups());
        Assert.Equal(time.GetUtcNow(), group.EnqueuedAt);
    }

    [Fact]
    public async Task GetStatus_ReportsDecayedHappiness_AndTheRestOfTheMood()
    {
        var (service, store, time) = CreateService();
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, groupSize: 3, Ct);
        var members = store.Find(rideId)!.SnapshotGroups().Single().Members;

        time.Advance(TimeSpan.FromMinutes(10));
        var reported = service.GetStatus(rideId).Groups.Single().People;

        Assert.Equal(members.Select(m => m.Number), reported.Select(p => p.Number));
        for (var i = 0; i < members.Count; i++)
        {
            Assert.Equal(members[i].Happiness - (5d * (Math.E - 1d)), reported[i].Happiness, Precision);
            Assert.Equal(members[i].PreferredG, reported[i].PreferredG);
            Assert.Equal(0d, reported[i].Nausea);
        }
    }

    [Fact]
    public async Task GetStatus_WithinTheGracePeriod_ReportsTheStartingHappiness()
    {
        var (service, store, time) = CreateService();
        var rideId = Guid.NewGuid();
        await service.EnqueueGroupAsync(rideId, groupSize: 1, Ct);
        var member = store.Find(rideId)!.SnapshotGroups().Single().Members.Single();

        time.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(member.Happiness, service.GetStatus(rideId).Groups.Single().People.Single().Happiness);
    }

    [Fact]
    public async Task TakeGroupAsync_FreezesTheHappinessAtTheMomentOfBoarding()
    {
        var (service, store, time) = CreateService();
        var rideId = Guid.NewGuid();
        var enqueued = (await service.EnqueueGroupAsync(rideId, groupSize: 2, Ct)).Single();
        var members = store.Find(rideId)!.SnapshotGroups().Single().Members;

        time.Advance(TimeSpan.FromMinutes(10));
        var taken = await service.TakeGroupAsync(rideId, enqueued.GroupId, Ct);

        Assert.NotNull(taken);
        for (var i = 0; i < members.Count; i++)
        {
            Assert.Equal(members[i].Happiness - (5d * (Math.E - 1d)), taken.People[i].Happiness, Precision);
        }
    }

    private static (RideQueueService Service, IRideQueueStore Store, FakeTimeProvider Time) CreateService()
    {
        var options = Options.Create(new QueueModuleOptions());
        var store = new InMemoryRideQueueStore(options);
        var publisher = new Mock<IIntegrationEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var time = new FakeTimeProvider(QueueTestData.Now);

        var service = new RideQueueService(
            store,
            QueueTestData.Generator(),
            publisher.Object,
            time,
            NullLogger<RideQueueService>.Instance);

        return (service, store, time);
    }
}
