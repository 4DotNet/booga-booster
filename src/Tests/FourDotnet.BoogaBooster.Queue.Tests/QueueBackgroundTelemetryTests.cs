using System.Diagnostics;
using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Filling;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the trace and metrics the Queue module's background work emits (ADR-0009):
/// a span per fill pass carrying the ride and what the pass added, the queued-groups
/// and queued-people counters, and an error status when a pass throws.
/// </summary>
public sealed class QueueBackgroundTelemetryTests : IDisposable
{
    private const string FillOperation = "FillRideQueue";
    private const string GroupsCounter = "boogabooster.queue.groups.queued";
    private const string PeopleCounter = "boogabooster.queue.people.queued";

    private readonly TelemetryRecorder _telemetry = new();

    public void Dispose() => _telemetry.Dispose();

    [Fact]
    public async Task AFillPass_TagsTheRide_AndWhatItAdded()
    {
        var rideId = Guid.NewGuid();
        var (filler, store) = Create(rideId, maxQueue: 500);

        using (_telemetry.Scope())
        {
            await filler.RunFillCycleAsync(TestContext.Current.CancellationToken);
        }

        var waiting = store.Find(rideId)!.PeopleWaiting;
        Assert.True(waiting > 0, "The pass should have seated somebody for this to be a useful assertion.");

        var activity = _telemetry.Activity(FillOperation);
        Assert.Equal(rideId, activity.GetTagItem("queue.ride.id"));
        Assert.Equal(waiting, activity.GetTagItem("queue.people.added"));
        Assert.True((int)activity.GetTagItem("queue.groups.added")! > 0);
    }

    [Fact]
    public async Task AFillPass_SpansEvenWhenItAddsNobody()
    {
        var rideId = Guid.NewGuid();

        // The worst weather floors the planned headcount to zero, so this pass adds
        // nobody — and a pass that adds nobody is exactly the one an operator asks
        // about, so it still gets a span.
        var (filler, store) = Create(rideId, maxQueue: 500, niceWeather: 0d);

        using (_telemetry.Scope())
        {
            await filler.RunFillCycleAsync(TestContext.Current.CancellationToken);
        }

        Assert.Null(store.Find(rideId));

        var activity = _telemetry.Activity(FillOperation);
        Assert.Equal(0, activity.GetTagItem("queue.groups.added"));
        Assert.Equal(0, activity.GetTagItem("queue.people.added"));
    }

    [Fact]
    public async Task EnqueueingAGroup_CountsTheGroupAndItsPeople()
    {
        // Enqueue one group of a distinctive size directly: measurements carry no trace
        // id, so the listener also sees what test classes running in parallel record,
        // and a size no other test uses is what isolates this assertion.
        const int GroupSize = 7;

        var rideId = Guid.NewGuid();
        var options = Options.Create(OptionsFor(rideId, maxQueue: 500));
        var queueService = new RideQueueService(
            new InMemoryRideQueueStore(options),
            QueueTestData.Generator(),
            SucceedingPublisher(),
            new FakeTimeProvider(),
            NullLogger<RideQueueService>.Instance);

        var enqueued = await queueService.EnqueueGroupAsync(
            rideId,
            GroupSize,
            TestContext.Current.CancellationToken);

        // Seven is well inside the boardable group size, so the party joins as one
        // group and the people counter records its size in a single measurement.
        var group = Assert.Single(enqueued);
        Assert.Equal(GroupSize, group.Size);

        var people = _telemetry.Measurement(PeopleCounter, measurement => measurement.Value == GroupSize);

        // Neither counter is tagged: ride id and group id are unbounded, so they stay
        // on the span and off the metric (design D6).
        Assert.Empty(people.Tags);
        Assert.All(_telemetry.Measurements(GroupsCounter), measurement => Assert.Empty(measurement.Tags));
    }

    [Fact]
    public async Task AFailingFillPass_IsRecordedAsAnError()
    {
        var rideId = Guid.NewGuid();
        var options = Options.Create(OptionsFor(rideId, maxQueue: 500));
        var queueService = new Mock<IRideQueueService>();
        queueService
            .Setup(service => service.EnqueueGroupAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("queue unavailable"));

        var filler = new RideQueueFillerService(
            queueService.Object,
            new InMemoryRideQueueStore(options),
            new WeatherInfluence(options),
            options,
            new FakeTimeProvider(),
            NullLogger<RideQueueFillerService>.Instance);

        using (_telemetry.Scope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => filler.RunFillCycleAsync(TestContext.Current.CancellationToken));
        }

        var activity = _telemetry.Activity(FillOperation);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("queue unavailable", activity.StatusDescription);
        Assert.Contains(activity.Events, activityEvent => activityEvent.Name == "exception");

        // The counts recorded before the failure survive, so the span shows how far
        // the pass got.
        Assert.Equal(0, activity.GetTagItem("queue.groups.added"));
    }

    private static IIntegrationEventPublisher SucceedingPublisher()
    {
        var publisher = new Mock<IIntegrationEventPublisher>();
        publisher
            .Setup(p => p.PublishAsync(It.IsAny<GroupQueuedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return publisher.Object;
    }

    private static (RideQueueFillerService Filler, IRideQueueStore Store) Create(
        Guid rideId,
        int maxQueue,
        double? niceWeather = null)
    {
        var options = Options.Create(OptionsFor(rideId, maxQueue));
        var store = new InMemoryRideQueueStore(options);

        IWeatherInfluence weather;
        if (niceWeather is { } reading)
        {
            var influence = new Mock<IWeatherInfluence>();
            influence.SetupGet(i => i.Current).Returns(reading);
            weather = influence.Object;
        }
        else
        {
            weather = new WeatherInfluence(options);
        }

        var time = new FakeTimeProvider();
        var queueService = new RideQueueService(
            store,
            QueueTestData.Generator(),
            SucceedingPublisher(),
            time,
            NullLogger<RideQueueService>.Instance);

        var filler = new RideQueueFillerService(
            queueService,
            store,
            weather,
            options,
            time,
            NullLogger<RideQueueFillerService>.Instance);

        return (filler, store);
    }

    private static QueueModuleOptions OptionsFor(Guid rideId, int maxQueue) => new()
    {
        RideIds = [rideId],
        MinArrivalsPerCycle = 4,
        MaxArrivalsPerCycle = 8,
        MinGroupSize = 1,
        MaxGroupSize = 5,
        MaxQueueLength = maxQueue,
        RandomSeed = 123,
    };
}
