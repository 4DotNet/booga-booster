using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using FourDotnet.BoogaBooster.Queue.Observability;

namespace FourDotnet.BoogaBooster.Queue.Features.GetQueueStatus;

/// <summary>
/// Returns the ride's waiting line as the queue service currently holds it. A
/// ride with no queue yet reports an empty line rather than failing, so an
/// operator can watch a ride before the filler has seated anyone.
/// </summary>
public sealed class GetQueueStatusQueryHandler : QueryHandler<GetQueueStatusQuery, GetQueueStatusResponse>
{
    private readonly IRideQueueService _queueService;

    public GetQueueStatusQueryHandler(IRideQueueService queueService)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
    }

    protected override Task<GetQueueStatusResponse> ExecuteAsync(
        GetQueueStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult(_queueService.GetStatus(query.RideId));
    }

    /// <summary>Records which ride was asked about.</summary>
    protected override void EnrichActivity(Activity activity, GetQueueStatusQuery query)
        => activity.SetTag(QueueTelemetryAttributes.RideId, query.RideId);

    /// <summary>
    /// Records how long the line was — counts only, never the queued people
    /// themselves, so no personal data reaches the span.
    /// </summary>
    protected override void EnrichActivityWithResponse(Activity activity, GetQueueStatusResponse response)
    {
        activity.SetTag(QueueTelemetryAttributes.GroupCount, response.GroupCount);
        activity.SetTag(QueueTelemetryAttributes.PeopleWaiting, response.PeopleWaiting);
    }
}
