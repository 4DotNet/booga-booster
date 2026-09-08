using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using FourDotnet.BoogaBooster.Queue.Features.GetQueueStatus;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace FourDotnet.BoogaBooster.Queue.Endpoints;

/// <summary>
/// HTTP endpoints owned by the Queue module (ADR-0007). The API host maps them by
/// calling <see cref="MapQueueEndpoints"/>; it contains no endpoint mappings itself.
/// Each endpoint maps its request to a command/query and dispatches to the injected
/// handler — no business logic lives here.
/// </summary>
public static class QueueEndpoints
{
    public static IEndpointRouteBuilder MapQueueEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/rides/{rideId:guid}/queue").WithTags("Queue");

        // Observe a ride's waiting line as it is filled by the background service.
        group.MapGet("/", async Task<Ok<GetQueueStatusResponse>> (
            Guid rideId,
            IQueryHandler<GetQueueStatusQuery, GetQueueStatusResponse> handler,
            CancellationToken cancellationToken) =>
        {
            var status = await handler.HandleAsync(new GetQueueStatusQuery(rideId), cancellationToken);
            return TypedResults.Ok(status);
        })
        .WithName("GetQueueStatus");

        // Subscribe to weather updates so the filler can track the crowd to the weather.
        endpoints.MapWeatherSubscription();

        return endpoints;
    }
}
