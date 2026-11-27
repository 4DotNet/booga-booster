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

/// <summary>
/// The single-riders arrival stream of <see cref="RideQueueFillerService"/>, driven
/// one cycle at a time with a seeded <see cref="QueueModuleOptions.RandomSeed"/>.
/// </summary>
public sealed class RideQueueFillerSingleRiderTests
{
    private static (RideQueueFillerService Filler, IRideQueueStore Store, Mock<IIntegrationEventPublisher> Publisher) Create(
        QueueModuleOptions options,
        double niceWeather = 1.0)
    {
        var opts = Options.Create(options);
        var time = new FakeTimeProvider();
        var store = new InMemoryRideQueueStore(opts);
        var publisher = new Mock<IIntegrationEventPublisher>();
        var weather = new Mock<IWeatherInfluence>();
        weather.SetupGet(w => w.Current).Returns(niceWeather);

        var queueService = new RideQueueService(
            store, QueueTestData.Generator(), publisher.Object, time, NullLogger<RideQueueService>.Instance);
        var filler = new RideQueueFillerService(
            queueService, store, weather.Object, opts, time, NullLogger<RideQueueFillerService>.Instance);

        return (filler, store, publisher);
    }

    private static QueueModuleOptions OptionsFor(
        Guid rideId,
        int minSingles = 1,
        int maxSingles = 3,
        int maxSingleQueue = 200,
        int seed = 123) => new()
    {
        RideIds = [rideId],
        MinArrivalsPerCycle = 4,
        MaxArrivalsPerCycle = 8,
        MinGroupSize = 1,
        MaxGroupSize = 5,
        MaxQueueLength = 500,
        MinSingleArrivalsPerCycle = minSingles,
        MaxSingleArrivalsPerCycle = maxSingles,
        MaxSingleRiderQueueLength = maxSingleQueue,
        RandomSeed = seed,
    };

    [Fact]
    public async Task FillCycle_AddsSingleRiders_WithinTheConfiguredBounds_EveryCycle()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, publisher) = Create(OptionsFor(rideId, minSingles: 1, maxSingles: 3));

        var previous = 0;
        for (var cycle = 0; cycle < 5; cycle++)
        {
            await filler.RunFillCycleAsync(CancellationToken.None);

            var waiting = store.FindSingles(rideId)!.PeopleWaiting;
            Assert.InRange(waiting - previous, 1, 3);
            previous = waiting;
        }

        publisher.Verify(
            p => p.PublishAsync(It.IsAny<SingleRiderQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(previous));
    }

    [Fact]
    public async Task FillCycle_InTheWorstWeather_AddsNoSingleRiders()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, publisher) = Create(OptionsFor(rideId, minSingles: 2, maxSingles: 3), niceWeather: 0.0);

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Null(store.FindSingles(rideId));
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<SingleRiderQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FillCycle_InTheBestWeather_AddsThePlannedNumber()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(OptionsFor(rideId, minSingles: 3, maxSingles: 3), niceWeather: 1.0);

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Equal(3, store.FindSingles(rideId)!.PeopleWaiting);
    }

    [Fact]
    public async Task FillCycle_StopsAtTheSingleRidersCapacity_AndDropsTheRest()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, publisher) = Create(OptionsFor(rideId, minSingles: 3, maxSingles: 3, maxSingleQueue: 2));

        await filler.RunFillCycleAsync(CancellationToken.None);
        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Equal(2, store.FindSingles(rideId)!.PeopleWaiting);
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<SingleRiderQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task FillCycle_AFullSinglesLine_DoesNotBlockGroupArrivals()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(OptionsFor(rideId, minSingles: 3, maxSingles: 3, maxSingleQueue: 1));
        store.GetOrCreateSingles(rideId).Enqueue(QueueTestData.Person(number: 999));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Equal(1, store.FindSingles(rideId)!.PeopleWaiting);
        Assert.InRange(store.Find(rideId)!.PeopleWaiting, 4, 8);
    }

    [Fact]
    public async Task FillCycle_SingleRiders_NeverJoinTheGroupQueue()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(OptionsFor(rideId, minSingles: 3, maxSingles: 3));

        await filler.RunFillCycleAsync(CancellationToken.None);

        var singleNumbers = store.FindSingles(rideId)!.SnapshotPeople().Select(p => p.Number);
        var groupNumbers = store.Find(rideId)!.SnapshotGroups().SelectMany(g => g.Members).Select(p => p.Number);
        Assert.Empty(singleNumbers.Intersect(groupNumbers));
    }

    [Fact]
    public async Task FillCycle_GroupArrivalsAreUnchanged_WithTheSinglesStreamEnabled()
    {
        var rideId = Guid.NewGuid();
        var (withoutSingles, storeWithout, _) = Create(OptionsFor(rideId, minSingles: 0, maxSingles: 0));
        var (withSingles, storeWith, _) = Create(OptionsFor(rideId, minSingles: 1, maxSingles: 3));

        var intervalsWithout = new List<TimeSpan>();
        var intervalsWith = new List<TimeSpan>();
        for (var cycle = 0; cycle < 5; cycle++)
        {
            await withoutSingles.RunFillCycleAsync(CancellationToken.None);
            await withSingles.RunFillCycleAsync(CancellationToken.None);
            intervalsWithout.Add(withoutSingles.PlanNextInterval());
            intervalsWith.Add(withSingles.PlanNextInterval());
        }

        // Same group sizes, in the same order, across every cycle — and the same pacing.
        Assert.Equal(
            storeWithout.Find(rideId)!.SnapshotGroups().Select(g => g.Size),
            storeWith.Find(rideId)!.SnapshotGroups().Select(g => g.Size));
        Assert.Equal(intervalsWithout, intervalsWith);
        Assert.Null(storeWithout.FindSingles(rideId));
        Assert.True(storeWith.FindSingles(rideId)!.PeopleWaiting >= 5);
    }

    [Fact]
    public async Task FillCycle_IsReproducible_ForTheSameSeed()
    {
        var rideId = Guid.NewGuid();
        var (first, firstStore, _) = Create(OptionsFor(rideId, seed: 7));
        var (second, secondStore, _) = Create(OptionsFor(rideId, seed: 7));

        for (var cycle = 0; cycle < 3; cycle++)
        {
            await first.RunFillCycleAsync(CancellationToken.None);
            await second.RunFillCycleAsync(CancellationToken.None);
        }

        Assert.Equal(
            firstStore.FindSingles(rideId)!.PeopleWaiting,
            secondStore.FindSingles(rideId)!.PeopleWaiting);
    }
}
