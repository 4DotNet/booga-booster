using System.Diagnostics;
using System.Diagnostics.Metrics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Core.Observability;
using Xunit;

namespace FourDotnet.BoogaBooster.Core.Tests;

/// <summary>
/// Covers the observability plumbing the CQRS base classes own (ADR-0009): every
/// handler spans from the shared activity source, tags what it was given, records
/// failures as errors, and counts and times every invocation. Each scenario uses its
/// own handler type so the operation name isolates it from tests running in parallel.
/// </summary>
public sealed class HandlerInstrumentationTests : IDisposable
{
    private readonly List<Activity> _activities = [];
    private readonly List<RecordedMeasurement> _measurements = [];
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    public HandlerInstrumentationTests()
    {
        _activityListener = new ActivityListener
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
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == BoogaBoosterTelemetry.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Record(instrument, value, tags));
        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }

    [Fact]
    public async Task CommandHandler_StartsActivity_TaggedWithOperationAndHandlerAttributes()
    {
        var handler = new RecordThingCommandHandler();

        await handler.HandleAsync(new RecordThingCommand(7), TestContext.Current.CancellationToken);

        Assert.Equal(7, handler.Recorded);
        var activity = SingleActivity("RecordThing");
        Assert.Equal("RecordThing", activity.GetTagItem("boogabooster.operation"));
        Assert.Equal("command", activity.GetTagItem("boogabooster.operation.kind"));
        Assert.Equal(7, activity.GetTagItem("test.amount"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public async Task CommandHandler_CountsAndTimes_SuccessfulInvocation()
    {
        await new RecordThingCommandHandler().HandleAsync(
            new RecordThingCommand(1),
            TestContext.Current.CancellationToken);

        var invocation = SingleMeasurement("boogabooster.handler.invocations", "RecordThing");
        Assert.Equal(1, invocation.Value);
        Assert.Equal("command", invocation.Tags["kind"]);
        Assert.Equal("ok", invocation.Tags["outcome"]);

        var duration = SingleMeasurement("boogabooster.handler.duration", "RecordThing");
        Assert.True(duration.Value >= 0);
        Assert.Equal("ok", duration.Tags["outcome"]);
    }

    [Fact]
    public async Task CommandHandler_RecordsFailure_OnSpanAndInMetrics()
    {
        var handler = new FailingCommandHandler();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new FailingCommand(), TestContext.Current.CancellationToken));

        Assert.Equal("boom", exception.Message);
        var activity = SingleActivity("Failing");
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("boom", activity.StatusDescription);
        Assert.Contains(activity.Events, activityEvent => activityEvent.Name == "exception");
        Assert.Equal("error", SingleMeasurement("boogabooster.handler.invocations", "Failing").Tags["outcome"]);
        Assert.Equal("error", SingleMeasurement("boogabooster.handler.duration", "Failing").Tags["outcome"]);
    }

    [Fact]
    public async Task CommandHandler_RejectsNullCommand_WithoutRecordingAnInvocation()
    {
        var handler = new RecordThingCommandHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!, TestContext.Current.CancellationToken));

        Assert.Empty(ActivitiesFor("RecordThing"));
        Assert.Empty(MeasurementsFor("boogabooster.handler.invocations", "RecordThing"));
    }

    [Fact]
    public async Task CommandHandler_UsesOverriddenOperationName()
    {
        await new CustomNameCommandHandler().HandleAsync(
            new RecordThingCommand(3),
            TestContext.Current.CancellationToken);

        Assert.Equal("ReticulateSplines", SingleActivity("ReticulateSplines").OperationName);
        Assert.Equal(1, SingleMeasurement("boogabooster.handler.invocations", "ReticulateSplines").Value);
    }

    [Fact]
    public async Task QueryHandler_TagsBothTheQueryAndWhatItRead()
    {
        var rideId = Guid.NewGuid();

        var response = await new ReadThingQueryHandler().HandleAsync(
            new ReadThingQuery(rideId),
            TestContext.Current.CancellationToken);

        Assert.Equal(42, response);
        var activity = SingleActivity("ReadThing");
        Assert.Equal("query", activity.GetTagItem("boogabooster.operation.kind"));
        Assert.Equal(rideId, activity.GetTagItem("test.ride.id"));
        Assert.Equal(42, activity.GetTagItem("test.count"));

        var invocation = SingleMeasurement("boogabooster.handler.invocations", "ReadThing");
        Assert.Equal("query", invocation.Tags["kind"]);
        Assert.Equal("ok", invocation.Tags["outcome"]);
    }

    [Fact]
    public async Task QueryHandler_RecordsFailure_AndLeavesResponseTagsOff()
    {
        var handler = new BrokenReadQueryHandler();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(new BrokenReadQuery(), TestContext.Current.CancellationToken));

        var activity = SingleActivity("BrokenRead");
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.GetTagItem("test.count"));
        Assert.Equal("error", SingleMeasurement("boogabooster.handler.invocations", "BrokenRead").Tags["outcome"]);
    }

    [Fact]
    public async Task QueryHandler_RejectsNullQuery_WithoutRecordingAnInvocation()
    {
        var handler = new ReadThingQueryHandler();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => handler.HandleAsync(null!, TestContext.Current.CancellationToken));

        Assert.Empty(ActivitiesFor("ReadThing"));
        Assert.Empty(MeasurementsFor("boogabooster.handler.invocations", "ReadThing"));
    }

    [Fact]
    public async Task Handler_WithoutOwnTags_IsStillTracedAndMetered()
    {
        await new PlainCommandHandler().HandleAsync(new PlainCommand(), TestContext.Current.CancellationToken);

        Assert.Equal("Plain", SingleActivity("Plain").OperationName);
        Assert.Equal(1, SingleMeasurement("boogabooster.handler.invocations", "Plain").Value);
    }

    private void Record(Instrument instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var snapshot = new Dictionary<string, object?>(tags.Length, StringComparer.Ordinal);

        foreach (var tag in tags)
        {
            snapshot[tag.Key] = tag.Value;
        }

        lock (_measurements)
        {
            _measurements.Add(new RecordedMeasurement(instrument.Name, value, snapshot));
        }
    }

    private Activity SingleActivity(string operation) => Assert.Single(ActivitiesFor(operation));

    private List<Activity> ActivitiesFor(string operation)
    {
        lock (_activities)
        {
            return [.. _activities.Where(activity => activity.OperationName == operation)];
        }
    }

    private RecordedMeasurement SingleMeasurement(string instrument, string operation)
        => Assert.Single(MeasurementsFor(instrument, operation));

    private List<RecordedMeasurement> MeasurementsFor(string instrument, string operation)
    {
        lock (_measurements)
        {
            return
            [
                .. _measurements.Where(measurement =>
                    measurement.Instrument == instrument
                    && Equals(measurement.Tags.GetValueOrDefault("operation"), operation)),
            ];
        }
    }

    private sealed record RecordedMeasurement(string Instrument, double Value, Dictionary<string, object?> Tags);

    private sealed record RecordThingCommand(int Amount) : Command;

    private sealed class RecordThingCommandHandler : CommandHandler<RecordThingCommand>
    {
        public int? Recorded { get; private set; }

        protected override Task ExecuteAsync(RecordThingCommand command, CancellationToken cancellationToken)
        {
            Recorded = command.Amount;
            return Task.CompletedTask;
        }

        protected override void EnrichActivity(Activity activity, RecordThingCommand command)
            => activity.SetTag("test.amount", command.Amount);
    }

    private sealed class CustomNameCommandHandler : CommandHandler<RecordThingCommand>
    {
        protected override string OperationName => "ReticulateSplines";

        protected override Task ExecuteAsync(RecordThingCommand command, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed record FailingCommand : Command;

    private sealed class FailingCommandHandler : CommandHandler<FailingCommand>
    {
        protected override Task ExecuteAsync(FailingCommand command, CancellationToken cancellationToken)
            => throw new InvalidOperationException("boom");
    }

    private sealed record PlainCommand : Command;

    private sealed class PlainCommandHandler : CommandHandler<PlainCommand>
    {
        protected override Task ExecuteAsync(PlainCommand command, CancellationToken cancellationToken)
            => Task.CompletedTask;
    }

    private sealed record ReadThingQuery(Guid RideId) : Query<int>;

    private sealed class ReadThingQueryHandler : QueryHandler<ReadThingQuery, int>
    {
        protected override Task<int> ExecuteAsync(ReadThingQuery query, CancellationToken cancellationToken)
            => Task.FromResult(42);

        protected override void EnrichActivity(Activity activity, ReadThingQuery query)
            => activity.SetTag("test.ride.id", query.RideId);

        protected override void EnrichActivityWithResponse(Activity activity, int response)
            => activity.SetTag("test.count", response);
    }

    private sealed record BrokenReadQuery : Query<int>;

    private sealed class BrokenReadQueryHandler : QueryHandler<BrokenReadQuery, int>
    {
        protected override Task<int> ExecuteAsync(BrokenReadQuery query, CancellationToken cancellationToken)
            => throw new InvalidOperationException("nope");
    }
}
