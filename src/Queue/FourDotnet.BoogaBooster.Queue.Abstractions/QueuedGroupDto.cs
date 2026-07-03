namespace FourDotnet.BoogaBooster.Queue.Abstractions;

/// <summary>
/// A group currently waiting in a ride's queue: its identity and the number of
/// people it contains (a group may be a single person).
/// </summary>
public sealed record QueuedGroupDto(Guid GroupId, int Size);
