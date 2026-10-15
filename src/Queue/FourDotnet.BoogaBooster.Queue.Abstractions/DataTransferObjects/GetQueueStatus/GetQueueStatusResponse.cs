namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

/// <summary>
/// The response for the GetQueueStatus feature: a snapshot of a ride's waiting
/// line — the groups in arrival order plus the aggregate group and people counts
/// and the waiting guests' average current happiness.
/// </summary>
/// <param name="RideId">The ride whose line this is.</param>
/// <param name="GroupCount">How many groups are waiting.</param>
/// <param name="PeopleWaiting">How many people are waiting across all groups.</param>
/// <param name="Groups">The waiting groups, front of the line first.</param>
/// <param name="AverageHappiness">
/// The average current happiness (0–100, queue decay included) of everyone
/// waiting, or <c>null</c> when nobody is waiting — absent rather than 0, so an
/// empty line is never mistaken for a very unhappy one.
/// </param>
public sealed record GetQueueStatusResponse(
    Guid RideId,
    int GroupCount,
    int PeopleWaiting,
    IReadOnlyList<QueuedGroupDto> Groups,
    double? AverageHappiness);
