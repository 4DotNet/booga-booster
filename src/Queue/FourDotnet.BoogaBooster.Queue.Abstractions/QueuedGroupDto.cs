namespace FourDotnet.BoogaBooster.Queue.Abstractions;

/// <summary>
/// A group currently waiting in a ride's queue: its identity and the people it
/// contains (a group may be a single person).
/// </summary>
public sealed record QueuedGroupDto(Guid GroupId, IReadOnlyList<PersonDto> People)
{
    /// <summary>The number of people in the group.</summary>
    public int Size => People.Count;

    /// <summary>The combined weight of everyone in the group, in kilograms.</summary>
    public int TotalWeightInKilograms => People.Sum(p => p.WeightInKilograms);
}
