using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

namespace FourDotnet.BoogaBooster.Queue.Features.GetQueueStatus;

/// <summary>Reads a snapshot of the given ride's waiting line.</summary>
/// <param name="RideId">The ride whose queue to read.</param>
public sealed record GetQueueStatusQuery(Guid RideId) : Query<GetQueueStatusResponse>;
