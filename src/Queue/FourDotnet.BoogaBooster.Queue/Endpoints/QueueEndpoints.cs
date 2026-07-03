using FourDotnet.BoogaBooster.Queue.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace FourDotnet.BoogaBooster.Queue.Endpoints;

/// <summary>
/// HTTP endpoints owned by the Queue module (ADR-0007). The API host maps them by
/// calling <see cref="MapQueueEndpoints"/>; it contains no endpoint mappings itself.
/// </summary>
public static class QueueEndpoints
{
    public static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/rides/{rideId:guid}/queue").WithTags("Queue");

        // Observe a ride's waiting line as it is filled by the background service.
        group.MapGet("/", Ok<QueueStatusDto> (Guid rideId, IRideQueueService queueService) =>
            TypedResults.Ok(queueService.GetStatus(rideId)));

        return endpoints;
    }
}
