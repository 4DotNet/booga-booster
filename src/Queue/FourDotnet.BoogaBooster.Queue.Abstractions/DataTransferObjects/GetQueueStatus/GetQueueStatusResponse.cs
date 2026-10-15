namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

/// <summary>
/// The response for the GetQueueStatus feature: a snapshot of a ride's two waiting
/// lines — the groups in arrival order with their aggregate counts, and the
/// single-riders line.
/// </summary>
/// <param name="RideId">The ride the snapshot belongs to.</param>
/// <param name="GroupCount">Number of groups waiting.</param>
/// <param name="PeopleWaiting">People waiting <em>in groups</em>; single riders are reported separately.</param>
/// <param name="Groups">The waiting groups, in arrival order.</param>
/// <param name="SingleRidersWaiting">Number of individuals waiting in the single-riders line.</param>
/// <param name="SingleRiders">The single riders, in arrival order.</param>
public sealed record GetQueueStatusResponse(
    Guid RideId,
    int GroupCount,
    int PeopleWaiting,
    IReadOnlyList<QueuedGroupDto> Groups,
    int SingleRidersWaiting,
    IReadOnlyList<PersonDto> SingleRiders)
{
    /// <summary>Everyone waiting on the ride, in groups or alone.</summary>
    public int TotalPeopleWaiting => PeopleWaiting + SingleRidersWaiting;
}
