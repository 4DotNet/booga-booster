namespace FourDotnet.BoogaBooster.Queue.Abstractions;

/// <summary>
/// A snapshot of a ride's waiting line: the groups in arrival order plus the
/// aggregate group and people counts.
/// </summary>
public sealed record QueueStatusDto(
    Guid RideId,
    int GroupCount,
    int PeopleWaiting,
    IReadOnlyList<QueuedGroupDto> Groups);
