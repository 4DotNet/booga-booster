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
    private static (RideQueueFillerService filler, IRideQueueStore store, Mock<IIntegrationEventPublisher> publisher) Create(
        QueueModuleOptions options,
        IWeatherInfluence? weatherInfluence = null)
    {
        var (filler, store, publisher, _) = CreateWithClock(options, weatherInfluence);
        return (filler, store, publisher);
    }

    private static (RideQueueFillerService filler, IRideQueueStore store, Mock<IIntegrationEventPublisher> publisher, FakeTimeProvider time) CreateWithClock(
        QueueModuleOptions options,
        IWeatherInfluence? weatherInfluence = null)
    {
        var opts = Options.Create(options);
        var time = new FakeTimeProvider();
        var store = new InMemoryRideQueueStore(opts);
        var publisher = new Mock<IIntegrationEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Default to the real state at its neutral pre-event value so weather-blind
        // tests fill at the base rate.
        weatherInfluence ??= new WeatherInfluence(opts);

        var queueService = new RideQueueService(
            store, QueueTestData.Generator(), publisher.Object, time, NullLogger<RideQueueService>.Instance);
        var filler = new RideQueueFillerService(
            queueService, store, weatherInfluence, opts, time, NullLogger<RideQueueFillerService>.Instance);

        return (filler, store, publisher, time);
    }

    private static IWeatherInfluence WeatherAt(double niceWeather)
    {
        var influence = new Mock<IWeatherInfluence>();
        influence.SetupGet(i => i.Current).Returns(niceWeather);
        return influence.Object;
    }

    private static QueueModuleOptions OptionsFor(Guid rideId, int min, int max, int maxQueue, int? seed = 123) => new()
    {
        RideIds = [rideId],
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

        // A zero-headcount group stream never even creates the ride's group queue.
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

    [Fact]
    public async Task FillCycle_BeforeAnyWeatherEvent_ScalesByNeutralDefault()
    {
        var rideId = Guid.NewGuid();
        // No weather override: the real WeatherInfluence sits at its neutral default
        // (1.0), and the default ceiling of 1.0 keeps the fill at the base rate.
        var (filler, store, _) = Create(OptionsFor(rideId, min: 6, max: 6, maxQueue: 500));

        await filler.RunFillCycleAsync(CancellationToken.None);

        // Scaled by the neutral default, not zeroed or left undefined.
        Assert.Equal(6, store.Find(rideId)!.PeopleWaiting);
    }

    [Fact]
    public async Task FillCycle_NiceWeather_EnqueuesMoreThanBadWeather()
    {
        // Same fixed base headcount (min == max) and seed for both runs, so only the
        // weather differs.
        var niceRide = Guid.NewGuid();
        var (niceFiller, niceStore, _) = Create(
            OptionsFor(niceRide, min: 20, max: 20, maxQueue: 500), WeatherAt(0.9));

        var badRide = Guid.NewGuid();
        var (badFiller, badStore, _) = Create(
            OptionsFor(badRide, min: 20, max: 20, maxQueue: 500), WeatherAt(0.2));

        await niceFiller.RunFillCycleAsync(CancellationToken.None);
        await badFiller.RunFillCycleAsync(CancellationToken.None);

        Assert.True(
            niceStore.Find(niceRide)!.PeopleWaiting > badStore.Find(badRide)!.PeopleWaiting,
            "Nice weather should draw a larger crowd than bad weather.");
    }

    [Fact]
    public async Task FillCycle_WorstWeather_EnqueuesNothing()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, publisher) = Create(
            OptionsFor(rideId, min: 20, max: 20, maxQueue: 500), WeatherAt(0.0));

        await filler.RunFillCycleAsync(CancellationToken.None);

        // Scaled headcount floors to zero: the ride's queue is never even created.
        Assert.Null(store.Find(rideId));
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FillCycle_ArrivalsRise_AsWeatherImproves()
    {
        // A fixed base of 20 and ceiling/exponent of 1.0 gives floor(20 * nice):
        // 0.25 -> 5, 0.5 -> 10, 0.9 -> 18 — strictly increasing.
        var counts = new List<int>();
        foreach (var nice in new[] { 0.25, 0.5, 0.9 })
        {
            var rideId = Guid.NewGuid();
            var (filler, store, _) = Create(
                OptionsFor(rideId, min: 20, max: 20, maxQueue: 500), WeatherAt(nice));

            await filler.RunFillCycleAsync(CancellationToken.None);
            counts.Add(store.Find(rideId)!.PeopleWaiting);
        }

        Assert.True(counts[0] < counts[1] && counts[1] < counts[2], "Arrivals should rise as the weather improves.");
    }

    [Fact]
    public async Task FillCycle_ScaledFill_StillFormsValidGroupsWithinBounds()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(
            OptionsFor(rideId, min: 20, max: 20, maxQueue: 500), WeatherAt(0.9));

        await filler.RunFillCycleAsync(CancellationToken.None);

        var queue = store.Find(rideId)!;
        var groups = queue.SnapshotGroups();
        Assert.NotEmpty(groups);
        Assert.All(groups, g => Assert.InRange(g.Size, 1, 5));
        // floor(20 * 0.9) = 18 people, all partitioned into valid groups.
        Assert.Equal(18, queue.PeopleWaiting);
    }

    [Fact]
    public async Task FillCycle_ScaledFill_StillStopsAtMaxQueueLength()
    {
        var rideId = Guid.NewGuid();
        // Nice weather would draw 20, but the queue caps at 5.
        var (filler, store, _) = Create(
            OptionsFor(rideId, min: 20, max: 20, maxQueue: 5), WeatherAt(1.0));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.True(store.Find(rideId)!.PeopleWaiting <= 5);
    }

    [Fact]
    public void NextInterval_InFairWeather_StaysWithinTenToThirtySeconds()
    {
        var (filler, _, _) = Create(
            OptionsFor(Guid.NewGuid(), min: 4, max: 8, maxQueue: 500), WeatherAt(0.9));

        for (var i = 0; i < 50; i++)
        {
            Assert.InRange(filler.PlanNextInterval(), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        }
    }

    [Fact]
    public void NextInterval_InBadWeather_StaysWithinThirtyToSixtySeconds()
    {
        var (filler, _, _) = Create(
            OptionsFor(Guid.NewGuid(), min: 4, max: 8, maxQueue: 500), WeatherAt(0.1));

        for (var i = 0; i < 50; i++)
        {
            Assert.InRange(filler.PlanNextInterval(), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
        }
    }

    [Fact]
    public void NextInterval_VariesAcrossCycles()
    {
        var (filler, _, _) = Create(
            OptionsFor(Guid.NewGuid(), min: 4, max: 8, maxQueue: 500), WeatherAt(0.9));

        var draws = Enumerable.Range(0, 20).Select(_ => filler.PlanNextInterval()).ToList();

        Assert.True(draws.Distinct().Count() > 1, "The interval should be re-drawn, not fixed.");
    }

    [Fact]
    public void NextInterval_FollowsTheLatestWeather()
    {
        var options = OptionsFor(Guid.NewGuid(), min: 4, max: 8, maxQueue: 500);
        var influence = new WeatherInfluence(Options.Create(options));
        var (filler, _, _) = Create(options, influence);

        influence.Update(0.9f);
        var fair = filler.PlanNextInterval();

        influence.Update(0.1f);
        var bad = filler.PlanNextInterval();

        Assert.InRange(fair, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));
        Assert.InRange(bad, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60));
    }

    // The single-riders arrival stream.

    private static QueueModuleOptions SinglesOptionsFor(
        Guid rideId,
        int minSingles,
        int maxSingles,
        int maxSinglesQueue = 200,
        int groupMin = 4,
        int groupMax = 8,
        int seed = 123)
    {
        var options = OptionsFor(rideId, groupMin, groupMax, maxQueue: 500, seed);
        options.MinSingleArrivalsPerCycle = minSingles;
        options.MaxSingleArrivalsPerCycle = maxSingles;
        options.MaxSingleRiderQueueLength = maxSinglesQueue;
        return options;
    }

    private static int SinglesWaiting(IRideQueueStore store, Guid rideId) =>
        store.FindSingles(rideId)?.PeopleWaiting ?? 0;

    [Fact]
    public async Task FillCycle_AddsIndividualsWithinTheConfiguredBounds_EveryCycle()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, publisher) = Create(SinglesOptionsFor(rideId, minSingles: 1, maxSingles: 3));

        var previous = 0;
        for (var cycle = 0; cycle < 20; cycle++)
        {
            await filler.RunFillCycleAsync(CancellationToken.None);

            var waiting = SinglesWaiting(store, rideId);
            Assert.InRange(waiting - previous, 1, 3);
            previous = waiting;
        }

        // Every individual is announced on their own topic.
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<SingleRiderQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(previous));
    }

    [Fact]
    public async Task FillCycle_InTheBestWeather_AdmitsThePlannedNumberOfIndividuals()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(
            SinglesOptionsFor(rideId, minSingles: 3, maxSingles: 3), WeatherAt(1.0));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Equal(3, SinglesWaiting(store, rideId));
    }

    [Fact]
    public async Task FillCycle_InTheWorstWeather_AddsNoIndividual()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, publisher) = Create(
            SinglesOptionsFor(rideId, minSingles: 3, maxSingles: 3), WeatherAt(0.0));

        await filler.RunFillCycleAsync(CancellationToken.None);

        // Scaled to zero: the single-riders line is never even created.
        Assert.Null(store.FindSingles(rideId));
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<SingleRiderQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task FillCycle_ScalesIndividualsByTheWeather_LikeGroups()
    {
        // A fixed base of 10 and the default linear response gives floor(10 * 0.5) = 5
        // for the singles, exactly as floor(20 * 0.5) = 10 for the people in groups.
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(
            SinglesOptionsFor(rideId, minSingles: 10, maxSingles: 10, groupMin: 20, groupMax: 20),
            WeatherAt(0.5));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Equal(5, SinglesWaiting(store, rideId));
        Assert.Equal(10, store.Find(rideId)!.PeopleWaiting);
    }

    [Fact]
    public async Task FillCycle_StopsAtTheSinglesCapacity_AndDropsTheRest()
    {
        var rideId = Guid.NewGuid();
        // Five individuals are planned but the line only holds two.
        var (filler, store, publisher) = Create(
            SinglesOptionsFor(rideId, minSingles: 5, maxSingles: 5, maxSinglesQueue: 2));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Equal(2, SinglesWaiting(store, rideId));
        publisher.Verify(
            p => p.PublishAsync(It.IsAny<SingleRiderQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // A later cycle against the full line adds nobody and does not fail.
        await filler.RunFillCycleAsync(CancellationToken.None);
        Assert.Equal(2, SinglesWaiting(store, rideId));
    }

    [Fact]
    public async Task FillCycle_AFullSinglesLine_DoesNotBlockGroupArrivals()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(
            SinglesOptionsFor(rideId, minSingles: 3, maxSingles: 3, maxSinglesQueue: 1));
        await filler.RunFillCycleAsync(CancellationToken.None);
        Assert.False(store.FindSingles(rideId)!.CanAccept(1));
        var groupPeopleBefore = store.Find(rideId)!.PeopleWaiting;

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.InRange(store.Find(rideId)!.PeopleWaiting - groupPeopleBefore, 4, 8);
    }

    [Fact]
    public async Task FillCycle_WithTheSinglesStreamSwitchedOff_AddsNoIndividual()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _) = Create(SinglesOptionsFor(rideId, minSingles: 0, maxSingles: 0));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Null(store.FindSingles(rideId));
        Assert.InRange(store.Find(rideId)!.PeopleWaiting, 4, 8);
    }

    [Fact]
    public async Task FillCycle_GroupArrivalsAreUnchanged_ByTheSinglesStream()
    {
        // Two fillers on the same seed: one with the singles stream switched off, one
        // with it on. The stream is planned after the groups and shares no headcount
        // budget with them, so the group sizes must match cycle for cycle.
        var withoutRide = Guid.NewGuid();
        var (without, withoutStore, _) = Create(SinglesOptionsFor(withoutRide, minSingles: 0, maxSingles: 0));

        var withRide = Guid.NewGuid();
        var (with, withStore, _) = Create(SinglesOptionsFor(withRide, minSingles: 1, maxSingles: 3));

        for (var cycle = 0; cycle < 10; cycle++)
        {
            await without.RunFillCycleAsync(CancellationToken.None);
            await with.RunFillCycleAsync(CancellationToken.None);

            Assert.Equal(
                withoutStore.Find(withoutRide)!.SnapshotGroups().Select(g => g.Size),
                withStore.Find(withRide)!.SnapshotGroups().Select(g => g.Size));
        }

        Assert.True(SinglesWaiting(withStore, withRide) >= 10, "The singles stream should have run every cycle.");
        Assert.Equal(0, SinglesWaiting(withoutStore, withoutRide));
    }

    [Fact]
    public async Task FillCycle_SinglesStream_IsReproducibleFromTheSeed()
    {
        var firstRide = Guid.NewGuid();
        var (first, firstStore, _) = Create(SinglesOptionsFor(firstRide, minSingles: 1, maxSingles: 3, seed: 77));
        var secondRide = Guid.NewGuid();
        var (second, secondStore, _) = Create(SinglesOptionsFor(secondRide, minSingles: 1, maxSingles: 3, seed: 77));

        for (var cycle = 0; cycle < 10; cycle++)
        {
            await first.RunFillCycleAsync(CancellationToken.None);
            await second.RunFillCycleAsync(CancellationToken.None);

            Assert.Equal(SinglesWaiting(firstStore, firstRide), SinglesWaiting(secondStore, secondRide));
        }
    }

    [Fact]
    public async Task FillCycle_ArrivingIndividuals_NeverJoinTheGroupQueue()
    {
        var rideId = Guid.NewGuid();
        // No group arrivals at all, so anything in the regular queue would be a leak.
        var (filler, store, _) = Create(
            SinglesOptionsFor(rideId, minSingles: 2, maxSingles: 2, groupMin: 0, groupMax: 0));

        await filler.RunFillCycleAsync(CancellationToken.None);

        Assert.Equal(2, SinglesWaiting(store, rideId));
        Assert.Null(store.Find(rideId));
    }

    /// <summary>
    /// Yields long enough for the hosted loop to reach its next <c>await</c> — the
    /// timer registration on start, or the continuation after a tick fires. The
    /// clock is fake, so this only bridges the thread-pool hand-off; it never waits
    /// out a real fill interval.
    /// </summary>
    private static Task SettleAsync() => Task.Delay(250);

    [Fact]
    public async Task Filler_InBadWeather_HoldsBackUntilTheBadWeatherBandIsReached()
    {
        var rideId = Guid.NewGuid();
        // A fixed base of 40 so the 0.1 weather multiplier still leaves four arrivals
        // to observe — this test is about when the cycle fires, not how big it is.
        var (filler, store, _, time) = CreateWithClock(
            OptionsFor(rideId, min: 40, max: 40, maxQueue: 500), WeatherAt(0.1));

        await filler.StartAsync(CancellationToken.None);
        try
        {
            await SettleAsync();

            // A fair-weather pace would have filled by now; bad weather holds the
            // first cycle back until at least thirty seconds have passed.
            time.Advance(TimeSpan.FromSeconds(29));
            await SettleAsync();
            Assert.Null(store.Find(rideId));

            // Past the top of the bad-weather band the cycle does fire.
            time.Advance(TimeSpan.FromSeconds(31));
            await SettleAsync();
            Assert.Equal(4, store.Find(rideId)!.PeopleWaiting);
        }
        finally
        {
            await filler.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Filler_InFairWeather_RunsACycleWithinThirtySeconds()
    {
        var rideId = Guid.NewGuid();
        var (filler, store, _, time) = CreateWithClock(
            OptionsFor(rideId, min: 4, max: 8, maxQueue: 500), WeatherAt(0.9));

        await filler.StartAsync(CancellationToken.None);
        try
        {
            await SettleAsync();

            // The top of the fair-weather band: a cycle must have fired by now.
            time.Advance(TimeSpan.FromSeconds(30));
            await SettleAsync();

            Assert.InRange(store.Find(rideId)!.PeopleWaiting, 4, 8);
        }
        finally
        {
            await filler.StopAsync(CancellationToken.None);
        }
    }
}
