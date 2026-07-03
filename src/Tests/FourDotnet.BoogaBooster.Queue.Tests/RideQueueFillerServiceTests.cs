using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Filling;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Exercises the hosted <see cref="RideQueueFillerService"/> fill behaviour by
/// invoking a single cycle directly, so the outcome is deterministic and does not
/// depend on driving the timer.
/// </summary>
public sealed class RideQueueFillerServiceTests
{
    private static (RideQueueFillerService filler, IRideQueueStore store, Mock<IIntegrationEventPublisher> publisher) Create(QueueModuleOptions options)
    {
        var opts = Options.Create(options);
        var time = new FakeTimeProvider();
        var store = new InMemoryRideQueueStore(opts);
        var publisher = new Mock<IIntegrationEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var queueService = new RideQueueService(
            store, QueueTestData.Generator(), publisher.Object, time, NullLogger<RideQueueService>.Instance);
        var filler = new RideQueueFillerService(
            queueService, store, opts, time, NullLogger<RideQueueFillerService>.Instance);

        return (filler, store, publisher);
    }

    private static QueueModuleOptions OptionsFor(Guid rideId, int min, int max, int maxQueue, int? seed = 123) => new()
    {
        RideIds = [rideId],
        FillInterval = TimeSpan.FromMinutes(1),
        MinArrivalsPerCycle = min,
        MaxArrivalsPerCycle = max,
        MinGroupSize = 1,
        MaxGroupSize = 5,
        MaxQueueLength = maxQueue,
        RandomSeed = seed,
    };

    [Fact]
    public async Task FillCycle_EnqueuesGuests_OverSuccessiveCycles()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, publisher) = Create(OptionsFor(rideId, min: 4, max: 8, maxQueue: 500));

        await filler.RunFillCycleAsync(CancellationToken.None);
        var afterFirst = store.Find(rideId)!.PeopleWaiting;

        await filler.RunFillCycleAsync(CancellationToken.None);
        var afterSecond = store.Find(rideId)!.PeopleWaiting;

        Assert.InRange(afterFirst, 4, 8);
        Assert.True(afterSecond > afterFirst, "The queue should grow across cycles.");
        // Every enqueued group is announced as an integration event.
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task FillCycle_SplitsArrivalsIntoValidGroups()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(OptionsFor(rideId, min: 4, max: 8, maxQueue: 500));

        await filler.RunFillCycleAsync(CancellationToken.None);

        var groups = store.Find(rideId)!.SnapshotGroups();
        Assert.NotEmpty(groups);
        Assert.All(groups, g => Assert.InRange(g.Size, 1, 5));
    }

    [Fact]
    public async Task FillCycle_WithZeroArrivalRate_EnqueuesNothing()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, publisher) = Create(OptionsFor(rideId, min: 0, max: 0, maxQueue: 500));

        await filler.RunFillCycleAsync(CancellationToken.None);

        // A zero-headcount cycle returns before it even creates the ride's queue.
        Assert.Null(store.Find(rideId));
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FillCycle_AtCapacity_AddsNoFurtherArrivals()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(OptionsFor(rideId, min: 4, max: 8, maxQueue: 4));

        // Pre-fill the queue to its maximum before the cycle runs.
        store.GetOrCreate(rideId).Enqueue(QueueTestData.Group(4));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Equal(4, store.Find(rideId)!.PeopleWaiting);
    }

    [Fact]
    public async Task FillCycle_NearCapacity_StopsAtTheMaximum()
    {
        var rideId = Guid.NewGuid();
        // Room for at most five people; a 4–8 cycle must stop the moment the next
        // group would overflow, never exceeding the maximum.
        var (filler, store, _) = Create(OptionsFor(rideId, min: 4, max: 8, maxQueue: 5));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.True(store.Find(rideId)!.PeopleWaiting <= 5);
    }

    [Fact]
    public async Task Filler_StartsAndStopsWithTheHost_WithoutThrowing()
    {
        var rideId = Guid.NewGuid();
        var (filler, _, _) = Create(OptionsFor(rideId, min: 4, max: 8, maxQueue: 500));

        await filler.StartAsync(CancellationToken.None);
        await filler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public void Filler_Constructs_WithNonDeterministicSeed_WhenNoSeedConfigured()
    {
        var (filler, _, _) = Create(OptionsFor(Guid.NewGuid(), min: 4, max: 8, maxQueue: 500, seed: null));

        Assert.NotNull(filler);
    }
}
