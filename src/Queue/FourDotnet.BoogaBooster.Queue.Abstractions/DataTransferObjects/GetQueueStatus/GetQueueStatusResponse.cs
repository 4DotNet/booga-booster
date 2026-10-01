namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

/// <summary>
/// The response for the GetQueueStatus feature: a snapshot of a ride's waiting
/// line — the groups in arrival order plus the aggregate group and people counts
/// and the average current happiness of everyone waiting.
/// </summary>
/// <param name="RideId">The ride whose line this is.</param>
/// <param name="GroupCount">How many groups are waiting.</param>
/// <param name="PeopleWaiting">How many people are waiting.</param>
/// <param name="Groups">The waiting groups, in arrival order.</param>
/// <param name="AverageHappiness">
/// The mean current (waited) happiness of every waiting person, or <c>null</c> when
/// nobody is waiting — absent rather than zero.
/// </param>
public sealed record GetQueueStatusResponse(
    Guid RideId,
    int GroupCount,
    int PeopleWaiting,
    IReadOnlyList<QueuedGroupDto> Groups,
    double? AverageHappiness);
