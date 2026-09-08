namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;

/// <summary>
/// A single person waiting in a ride's queue: their unique number, name and
/// weight in kilograms.
/// </summary>
/// <remarks>
/// Shared by several of the module's features and by
/// <see cref="IRideQueueService"/>, so it sits directly under
/// <c>DataTransferObjects</c> rather than inside one feature's namespace.
/// </remarks>
public sealed record PersonDto(long Number, string Name, int WeightInKilograms);
