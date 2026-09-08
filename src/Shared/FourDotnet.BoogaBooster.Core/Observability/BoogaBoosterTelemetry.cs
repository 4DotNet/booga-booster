using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace FourDotnet.BoogaBooster.Core.Observability;

/// <summary>
/// The solution's one <see cref="System.Diagnostics.ActivitySource"/> and
/// <see cref="System.Diagnostics.Metrics.Meter"/> (ADR-0009), plus the handler
/// instruments the CQRS base classes emit. Both carry the same well-known name so
/// the Aspire <c>ServiceDefaults</c> project registers a single source and a single
/// meter; instrumentation is never configured per module or per host.
/// </summary>
public static class BoogaBoosterTelemetry
{
    /// <summary>
    /// The well-known name shared by the activity source and the meter. Referenced
    /// by <c>ServiceDefaults</c> so tracing and metrics collect what we emit.
    /// </summary>
    public const string SourceName = "FourDotnet.BoogaBooster";

    private static readonly string SourceVersion =
        typeof(BoogaBoosterTelemetry).Assembly.GetName().Version?.ToString() ?? "unknown";

    /// <summary>The activity source every command and query handler spans from.</summary>
    public static readonly ActivitySource ActivitySource = new(SourceName, SourceVersion);

    /// <summary>The meter every module publishes its metrics through.</summary>
    public static readonly Meter Meter = new(SourceName, SourceVersion);

    /// <summary>
    /// Counts handler invocations, tagged with the operation, its kind and its
    /// outcome — deliberately low cardinality so the counter stays aggregatable.
    /// </summary>
    public static readonly Counter<long> HandlerInvocations = Meter.CreateCounter<long>(
        "boogabooster.handler.invocations",
        unit: "{invocation}",
        description: "Number of command and query handler invocations.");

    /// <summary>How long each handler invocation took, tagged identically to the counter.</summary>
    public static readonly Histogram<double> HandlerDuration = Meter.CreateHistogram<double>(
        "boogabooster.handler.duration",
        unit: "ms",
        description: "Duration of command and query handler invocations.");
}
