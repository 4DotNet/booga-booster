using System.Diagnostics;
using System.Diagnostics.Metrics;
using FourDotnet.BoogaBooster.Core.Observability;
using Xunit;

namespace FourDotnet.BoogaBooster.IntegrationMessages.Tests;

/// <summary>
/// Collects the activities and measurements this module emits through the shared
/// source and meter (ADR-0009), following the <c>ActivityListener</c> pattern
/// <c>QueueHandlerTelemetryTests</c> established and the built-in
/// <see cref="MeterListener"/> pattern <c>HandlerInstrumentationTests</c> established —
/// no extra test package.
/// </summary>
/// <remarks>
/// The activity source and meter are process-wide, so a listener also sees what test
/// classes running in parallel emit. Activities are therefore isolated by trace:
/// <see cref="Scope"/> opens a root activity, everything the test then starts inherits
/// its trace id, and the activity lookups only consider that trace. Measurements carry
/// no trace id, so the metric lookups assert that a matching measurement exists rather
/// than that it is the only one — enough to fail when an instrument stops being
/// recorded or is tagged wrongly, without depending on what a sibling test emitted.
/// </remarks>
internal sealed class TelemetryRecorder : IDisposable
{
    private readonly List<Activity> _activities = [];
    private readonly List<RecordedMeasurement> _measurements = [];
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    private ActivityTraceId _scopedTrace;

    internal TelemetryRecorder()
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

    /// <summary>
    /// Opens a root activity whose trace the activity lookups are confined to.
    /// Dispose it (a <c>using</c> in the test body) when the work under test is done.
    /// </summary>
    internal Activity Scope(string name = "TestScope")
    {
        var scope = BoogaBoosterTelemetry.ActivitySource.StartActivity(name);
        Assert.NotNull(scope);
        _scopedTrace = scope.TraceId;
        return scope;
    }

    /// <summary>Every completed activity for <paramref name="operation"/> in the open scope.</summary>
    internal List<Activity> Activities(string operation)
    {
        lock (_activities)
        {
            return
            [
                .. _activities.Where(activity =>
                    activity.TraceId == _scopedTrace && activity.OperationName == operation),
            ];
        }
    }

    /// <summary>The one completed activity for <paramref name="operation"/> in the open scope.</summary>
    internal Activity Activity(string operation) => Assert.Single(Activities(operation));

    /// <summary>Every measurement of <paramref name="instrument"/> this listener saw.</summary>
    internal List<RecordedMeasurement> Measurements(string instrument)
    {
        lock (_measurements)
        {
            return [.. _measurements.Where(measurement => measurement.Instrument == instrument)];
        }
    }

    /// <summary>
    /// A measurement of <paramref name="instrument"/> matching <paramref name="match"/>,
    /// asserting at least one exists. See the remarks on why this is not
    /// <c>Assert.Single</c>.
    /// </summary>
    internal RecordedMeasurement Measurement(string instrument, Func<RecordedMeasurement, bool> match)
    {
        var matching = Measurements(instrument).Where(match).ToList();
        Assert.NotEmpty(matching);
        return matching[0];
    }

    /// <summary>A measurement of <paramref name="instrument"/> tagged as given.</summary>
    internal RecordedMeasurement Measurement(string instrument, params (string Key, object? Value)[] tags)
        => Measurement(
            instrument,
            measurement => tags.All(tag => Equals(measurement.Tags.GetValueOrDefault(tag.Key), tag.Value)));

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
}

/// <summary>One recorded measurement: which instrument, what value, and its tags.</summary>
internal sealed record RecordedMeasurement(string Instrument, double Value, Dictionary<string, object?> Tags);
