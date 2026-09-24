using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.GetRideTelemetry;

/// <summary>Returns the current telemetry from the ride store.</summary>
public sealed class GetRideTelemetryQueryHandler : QueryHandler<GetRideTelemetryQuery, RideTelemetry>
{
    private readonly IRideStore _store;

    public GetRideTelemetryQueryHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task<RideTelemetry> ExecuteAsync(GetRideTelemetryQuery query, CancellationToken cancellationToken)
        => Task.FromResult(_store.GetTelemetry());

    /// <summary>
    /// Summarises the snapshot with the scalars an operator would read first — the
    /// lifecycle state, how many people are aboard, how fast the mill is turning, and
    /// how the riders feel on average. The sixteen gondolas and four hubs stay off the
    /// span: the payload belongs in the response, not in the trace. The mood
    /// attributes are aggregates only and are omitted when nobody is seated.
    /// </summary>
    protected override void EnrichActivityWithResponse(Activity activity, RideTelemetry response)
    {
        activity.SetTag(RideTelemetryAttributes.State, response.State.ToString());
        activity.SetTag(RideTelemetryAttributes.PassengersBoarded, response.BoardedPassengerCount);
        activity.SetTag(RideTelemetryAttributes.MillRpm, response.Mill.Rpm);

        if (response.Riders.AverageHappiness is { } happiness)
        {
            activity.SetTag(RideTelemetryAttributes.RidersHappinessAverage, happiness);
        }

        if (response.Riders.AverageNausea is { } nausea)
        {
            activity.SetTag(RideTelemetryAttributes.RidersNauseaAverage, nausea);
        }
    }
}
