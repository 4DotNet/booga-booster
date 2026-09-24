namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

/// <summary>
/// The response for the GetQueueStatus feature: a snapshot of a ride's waiting
/// line — the groups in arrival order plus the aggregate group and people counts
/// and the average current happiness of everyone waiting.
/// </summary>
/// <param name="AverageHappiness">
/// The mean current happiness of every person in the line, in <c>[0, 1]</c>, or
/// <c>null</c> when nobody is waiting.
/// </param>
public sealed record GetQueueStatusResponse(
    Guid RideId,
    int GroupCount,
    int PeopleWaiting,
    IReadOnlyList<QueuedGroupDto> Groups,
    double? AverageHappiness);
