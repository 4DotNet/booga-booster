namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

/// <summary>
/// The response for the GetQueueStatus feature: a snapshot of a ride's two waiting
/// lines — the groups in arrival order with the group and people counts, and the
/// single-riders line with its own count.
/// </summary>
/// <param name="RideId">The ride the snapshot describes.</param>
/// <param name="GroupCount">The number of groups waiting in the regular queue.</param>
/// <param name="PeopleWaiting">
/// The number of people waiting <em>in groups</em>. Single riders are never counted
/// here; they are reported separately as <paramref name="SingleRidersWaiting"/>.
/// </param>
/// <param name="Groups">The waiting groups, in arrival order.</param>
/// <param name="SingleRidersWaiting">The number of individuals waiting in the single-riders line.</param>
/// <param name="SingleRiders">The waiting single riders, in arrival order.</param>
public sealed record GetQueueStatusResponse(
    Guid RideId,
    int GroupCount,
    int PeopleWaiting,
    IReadOnlyList<QueuedGroupDto> Groups,
    int SingleRidersWaiting,
    IReadOnlyList<PersonDto> SingleRiders)
{
    /// <summary>Everyone waiting for the ride: people in groups plus single riders.</summary>
    public int TotalPeopleWaiting => PeopleWaiting + SingleRidersWaiting;
}
