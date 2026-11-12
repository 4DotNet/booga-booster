namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

/// <summary>
/// The response for the GetQueueStatus feature: a snapshot of both of a ride's
/// waiting lines — the groups in arrival order with their aggregate counts, and
/// the single-riders line beside them.
/// </summary>
/// <param name="RideId">The ride the snapshot describes.</param>
/// <param name="GroupCount">How many groups are waiting in the regular queue.</param>
/// <param name="PeopleWaiting">
/// How many people are waiting <em>in groups</em>. Single riders are never counted
/// here; they are reported as <paramref name="SingleRidersWaiting"/>.
/// </param>
/// <param name="Groups">The waiting groups, in arrival order.</param>
/// <param name="SingleRidersWaiting">How many individuals are waiting in the single-riders line.</param>
/// <param name="SingleRiders">The waiting single riders, in arrival order.</param>
public sealed record GetQueueStatusResponse(
    Guid RideId,
    int GroupCount,
    int PeopleWaiting,
    IReadOnlyList<QueuedGroupDto> Groups,
    int SingleRidersWaiting,
    IReadOnlyList<PersonDto> SingleRiders)
{
    /// <summary>Everyone waiting for the ride: the people in groups plus the single riders.</summary>
    public int TotalPeopleWaiting => PeopleWaiting + SingleRidersWaiting;
}
