namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;

/// <summary>
/// A group currently waiting in a ride's queue: its identity and the people it
/// contains (a group may be a single person).
/// </summary>
/// <remarks>
/// Shared by several of the module's features and by
/// <see cref="IRideQueueService"/>, so it sits directly under
/// <c>DataTransferObjects</c> rather than inside one feature's namespace.
/// </remarks>
public sealed record QueuedGroupDto(Guid GroupId, IReadOnlyList<PersonDto> People)
{
    /// <summary>The number of people in the group.</summary>
    public int Size => People.Count;

    /// <summary>The combined weight of everyone in the group, in kilograms.</summary>
    public int TotalWeightInKilograms => People.Sum(p => p.WeightInKilograms);
}
