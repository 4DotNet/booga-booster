using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using FourDotnet.BoogaBooster.Queue.Features.GetQueueStatus;
using FourDotnet.BoogaBooster.Queue.Features.RecordWeatherUpdate;
using FourDotnet.BoogaBooster.Queue.Filling;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the span attributes the Queue feature handlers add on top of the base-class
/// instrumentation (ADR-0009) — the ride asked about, how long its line was, and both
/// sides of a weather update — and confirms no queued person reaches a span.
/// </summary>
public sealed class QueueHandlerTelemetryTests : IDisposable
{
    private readonly List<Activity> _activities = [];
    private readonly ActivityListener _listener;

    public QueueHandlerTelemetryTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BoogaBoosterTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (_activities)
                {
                    _activities.Add(activity);
                }
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task GetQueueStatus_TagsTheRide_AndTheLineItRead()
    {
        var rideId = Guid.NewGuid();
        var person = new PersonDto(1, "Person 1", 80, PreferredIntensity: 0.5, Happiness: 0.75, Nausea: 0);
        var snapshot = new GetQueueStatusResponse(
            rideId,
            GroupCount: 1,
            PeopleWaiting: 1,
            Groups: [new QueuedGroupDto(Guid.NewGuid(), [person])],
            AverageHappiness: 0.75);
        var service = new Mock<IRideQueueService>();
        service.Setup(s => s.GetStatus(rideId)).Returns(snapshot);

        await new GetQueueStatusQueryHandler(service.Object).HandleAsync(
            new GetQueueStatusQuery(rideId),
            TestContext.Current.CancellationToken);

        var activity = ActivityFor("GetQueueStatus", "queue.ride.id", rideId);
        Assert.Equal(rideId, activity.GetTagItem("queue.ride.id"));
        Assert.Equal(1, activity.GetTagItem("queue.group.count"));
        Assert.Equal(1, activity.GetTagItem("queue.people.waiting"));
        Assert.DoesNotContain(
            activity.Tags,
            tag => tag.Value is not null && tag.Value.Contains(person.Name, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetQueueStatus_TagsTheAverageHappiness_AndNeverAPerson()
    {
        // Two people whose own happiness values differ from the average, so a tag that
        // leaked an individual's mood would be distinguishable from the aggregate.
        var rideId = Guid.NewGuid();
        var grumpy = new PersonDto(7, "Grumpy Guest", 80, PreferredIntensity: 0.3, Happiness: 0.6, Nausea: 0);
        var cheerful = new PersonDto(8, "Cheerful Guest", 70, PreferredIntensity: 0.9, Happiness: 0.8, Nausea: 0);
        var snapshot = new GetQueueStatusResponse(
            rideId,
            GroupCount: 1,
            PeopleWaiting: 2,
            Groups: [new QueuedGroupDto(Guid.NewGuid(), [grumpy, cheerful])],
            AverageHappiness: 0.7);
        var service = new Mock<IRideQueueService>();
        service.Setup(s => s.GetStatus(rideId)).Returns(snapshot);

        await new GetQueueStatusQueryHandler(service.Object).HandleAsync(
            new GetQueueStatusQuery(rideId),
            TestContext.Current.CancellationToken);

        var activity = ActivityFor("GetQueueStatus", "queue.ride.id", rideId);
        Assert.Equal(0.7, activity.GetTagItem("queue.happiness.average"));

        foreach (var person in new[] { grumpy, cheerful })
        {
            Assert.DoesNotContain(activity.TagObjects, tag => Equals(tag.Value, person.Happiness));
            Assert.DoesNotContain(activity.TagObjects, tag => Equals(tag.Value, person.Number));
            Assert.DoesNotContain(
                activity.Tags,
                tag => tag.Value is not null && tag.Value.Contains(person.Name, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task GetQueueStatus_ForAnEmptyLine_OmitsTheAverageHappinessTag()
    {
        var rideId = Guid.NewGuid();
        var snapshot = new GetQueueStatusResponse(
            rideId,
            GroupCount: 0,
            PeopleWaiting: 0,
            Groups: [],
            AverageHappiness: null);
        var service = new Mock<IRideQueueService>();
        service.Setup(s => s.GetStatus(rideId)).Returns(snapshot);

        await new GetQueueStatusQueryHandler(service.Object).HandleAsync(
            new GetQueueStatusQuery(rideId),
            TestContext.Current.CancellationToken);

        var activity = ActivityFor("GetQueueStatus", "queue.ride.id", rideId);
        Assert.DoesNotContain(activity.TagObjects, tag => tag.Key == "queue.happiness.average");
    }

    [Fact]
    public async Task RecordWeatherUpdate_TagsTheObservedReading_AndTheValueItReplaced()
    {
        var influence = new WeatherInfluence(
            Options.Create(new QueueModuleOptions { NeutralWeatherDefault = 1.0 }));

        await new RecordWeatherUpdateCommandHandler(influence).HandleAsync(
            new RecordWeatherUpdateCommand(0.3125f),
            TestContext.Current.CancellationToken);

        var activity = ActivityFor("RecordWeatherUpdate", "queue.weather.nice_weather.observed", 0.3125f);
        Assert.Equal(0.3125f, activity.GetTagItem("queue.weather.nice_weather.observed"));
        Assert.Equal(1.0, activity.GetTagItem("queue.weather.nice_weather.previous"));
    }

    /// <summary>
    /// The activity source is process-wide, so the listener also sees handlers other
    /// test classes run in parallel. Match on a tag value unique to this test.
    /// </summary>
    private Activity ActivityFor(string operation, string tag, object expected)
    {
        lock (_activities)
        {
            return Assert.Single(_activities.Where(activity =>
                activity.OperationName == operation && Equals(activity.GetTagItem(tag), expected)));
        }
    }
}
