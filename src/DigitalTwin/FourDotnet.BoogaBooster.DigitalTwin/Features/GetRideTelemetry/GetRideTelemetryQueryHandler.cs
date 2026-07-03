using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.GetRideTelemetry;

/// <summary>Returns the current telemetry from the ride store.</summary>
public sealed class GetRideTelemetryQueryHandler : QueryHandler<GetRideTelemetryQuery, RideTelemetry>
{
    private readonly IRideStore _store;

    public GetRideTelemetryQueryHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public override Task<RideTelemetry> HandleAsync(GetRideTelemetryQuery query, CancellationToken cancellationToken)
        => Task.FromResult(_store.GetTelemetry());
}
