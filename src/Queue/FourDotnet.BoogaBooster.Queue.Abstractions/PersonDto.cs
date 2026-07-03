namespace FourDotnet.BoogaBooster.Queue.Abstractions;

/// <summary>
/// A single person waiting in a ride's queue: their unique number, name and
/// weight in kilograms.
/// </summary>
public sealed record PersonDto(long Number, string Name, int WeightInKilograms);
