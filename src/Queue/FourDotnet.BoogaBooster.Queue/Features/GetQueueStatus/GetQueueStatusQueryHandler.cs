using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

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

    public override Task<GetQueueStatusResponse> HandleAsync(
        GetQueueStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return Task.FromResult(_queueService.GetStatus(query.RideId));
    }
}
