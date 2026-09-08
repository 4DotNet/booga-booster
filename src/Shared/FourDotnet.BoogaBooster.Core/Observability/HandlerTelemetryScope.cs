using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FourDotnet.BoogaBooster.Core.Observability;

/// <summary>
/// The instrumentation plumbing shared by the CQRS base classes (ADR-0009): it starts
/// the activity, times the invocation and records the outcome counter and duration
/// histogram, so a concrete handler only adds its own tags and domain metrics.
/// A mutable struct rather than a class so an instrumented handler allocates nothing.
/// </summary>
internal struct HandlerTelemetryScope
{
    private readonly Activity? _activity;
    private readonly long _startedAt;
    private readonly string _operation;
    private readonly string _kind;
    private string _outcome;

    private HandlerTelemetryScope(Activity? activity, string operation, string kind)
    {
        _activity = activity;
        _operation = operation;
        _kind = kind;
        _outcome = TelemetryOutcome.Ok;
        _startedAt = Stopwatch.GetTimestamp();
    }

    /// <summary>The started activity, or <c>null</c> when nothing is listening.</summary>
    internal readonly Activity? Activity => _activity;

    /// <summary>Starts the activity for <paramref name="operation"/> and begins timing.</summary>
    internal static HandlerTelemetryScope Start(string operation, string kind)
    {
        var activity = BoogaBoosterTelemetry.ActivitySource.StartActivity(operation);

        if (activity is not null)
        {
            activity.SetTag(TelemetryTags.SpanOperation, operation);
            activity.SetTag(TelemetryTags.SpanOperationKind, kind);
        }

        return new HandlerTelemetryScope(activity, operation, kind);
    }

    /// <summary>
    /// Marks the invocation failed: the exception lands on the span and the outcome
    /// tag flips to <c>error</c> before <see cref="Complete"/> records the metrics.
    /// </summary>
    internal void Failed(Exception exception)
    {
        _outcome = TelemetryOutcome.Error;
        _activity?.AddException(exception);
        _activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
    }

    /// <summary>Records the counter and histogram and ends the activity.</summary>
    internal readonly void Complete()
    {
        var tags = new TagList
        {
            { TelemetryTags.Operation, _operation },
            { TelemetryTags.Kind, _kind },
            { TelemetryTags.Outcome, _outcome },
        };

        BoogaBoosterTelemetry.HandlerInvocations.Add(1, tags);
        BoogaBoosterTelemetry.HandlerDuration.Record(
            Stopwatch.GetElapsedTime(_startedAt).TotalMilliseconds,
            tags);

        _activity?.Dispose();
    }
}
