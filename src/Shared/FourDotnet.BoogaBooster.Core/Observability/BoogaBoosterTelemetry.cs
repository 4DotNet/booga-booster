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

    /// <summary>
    /// Counts physics ticks the ride simulation loop executed. The loop runs at 120 Hz,
    /// which is why it is counted rather than spanned — a span per tick would swamp the
    /// trace backend and bury the one tick worth looking at.
    /// </summary>
    public static readonly Counter<long> SimulationTicks = Meter.CreateCounter<long>(
        "boogabooster.ride.simulation.ticks",
        unit: "{tick}",
        description: "Number of fixed-step physics ticks the ride simulation executed.");

    /// <summary>
    /// How long a single physics tick took. Against the 8.33 ms budget of a 120 Hz step,
    /// this is the instrument that shows the loop falling behind.
    /// </summary>
    public static readonly Histogram<double> SimulationTickDuration = Meter.CreateHistogram<double>(
        "boogabooster.ride.simulation.tick.duration",
        unit: "ms",
        description: "Duration of a single ride-simulation physics tick.");

    /// <summary>Counts passengers boarded, untagged by seat so the series stays single.</summary>
    public static readonly Counter<long> PassengersBoarded = Meter.CreateCounter<long>(
        "boogabooster.ride.passengers.boarded",
        unit: "{passenger}",
        description: "Number of passengers boarded onto a gondola seat.");

    /// <summary>
    /// Counts ride-state transition requests, tagged by outcome (<c>accepted</c> /
    /// <c>rejected</c>) and by the requested target state — seven values, so a dashboard
    /// can show which transition the lifecycle guard rejects most.
    /// </summary>
    public static readonly Counter<long> RideStateTransitions = Meter.CreateCounter<long>(
        "boogabooster.ride.state.transitions",
        unit: "{transition}",
        description: "Number of ride lifecycle transition requests, by outcome and target state.");

    /// <summary>Counts groups joining a ride queue.</summary>
    public static readonly Counter<long> QueueGroupsQueued = Meter.CreateCounter<long>(
        "boogabooster.queue.groups.queued",
        unit: "{group}",
        description: "Number of groups added to a ride queue.");

    /// <summary>Counts the people in those groups — a group carries between one and eight.</summary>
    public static readonly Counter<long> QueuePeopleQueued = Meter.CreateCounter<long>(
        "boogabooster.queue.people.queued",
        unit: "{person}",
        description: "Number of people added to a ride queue.");

    /// <summary>
    /// Counts weather disturbances an operator raised, tagged by kind
    /// (<c>precipitation</c> / <c>strong-wind</c>).
    /// </summary>
    public static readonly Counter<long> WeatherDisturbances = Meter.CreateCounter<long>(
        "boogabooster.weather.disturbances",
        unit: "{disturbance}",
        description: "Number of weather disturbances raised, by kind.");

    /// <summary>
    /// Counts integration events handed to the transport, tagged by topic and outcome
    /// (<c>ok</c> / <c>error</c>) — both bounded sets, so a topic that starts failing is
    /// visible without a trace search.
    /// </summary>
    public static readonly Counter<long> IntegrationEventsPublished = Meter.CreateCounter<long>(
        "boogabooster.messaging.events.published",
        unit: "{event}",
        description: "Number of integration events published, by topic and outcome.");
}
