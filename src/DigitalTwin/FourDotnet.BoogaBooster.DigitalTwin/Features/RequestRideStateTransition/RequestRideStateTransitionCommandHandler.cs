using System.Diagnostics;
using System.Diagnostics.Metrics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.RequestRideStateTransition;

/// <summary>Requests a lifecycle transition through the store; the domain rejects an illegal one.</summary>
public sealed class RequestRideStateTransitionCommandHandler : CommandHandler<RequestRideStateTransitionCommand>
{
    private readonly IRideStore _store;

    public RequestRideStateTransitionCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(
        RequestRideStateTransitionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        try
        {
            _store.RequestStateTransition(command.Target);
        }
        catch
        {
            // A guard refusing a transition is an expected outcome, not an anomaly,
            // and it is the one an operator most wants counted. The base class still
            // records the throw as an error on the span and in the handler counter.
            RecordTransition(command.Target, TelemetryOutcome.Rejected);
            throw;
        }

        RecordTransition(command.Target, TelemetryOutcome.Accepted);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Records the state asked for and the state the ride was in. Both are set before
    /// the store is called, so they survive on the span when the guard refuses — which
    /// is exactly the case worth tracing.
    /// </summary>
    protected override void EnrichActivity(Activity activity, RequestRideStateTransitionCommand command)
    {
        activity.SetTag(RideTelemetryAttributes.StateRequested, command.Target.ToString());
        activity.SetTag(RideTelemetryAttributes.State, _store.CurrentState.ToString());
    }

    /// <summary>
    /// Both tags are drawn from fixed sets — seven lifecycle states and two outcomes —
    /// so tagging the target state costs at most fourteen series and lets a dashboard
    /// show which transition the guard refuses most.
    /// </summary>
    private static void RecordTransition(Abstractions.RideState target, string outcome)
        => BoogaBoosterTelemetry.RideStateTransitions.Add(
            1,
            new TagList
            {
                { RideTelemetryAttributes.StateRequested, target.ToString() },
                { TelemetryTags.Outcome, outcome },
            });
}
