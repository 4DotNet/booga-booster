namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;

/// <summary>
/// A single person waiting in a ride's queue: their unique number, name, weight
/// in kilograms and rider profile.
/// </summary>
/// <param name="Number">The person's process-unique number.</param>
/// <param name="Name">The person's full name.</param>
/// <param name="WeightInKilograms">The person's weight in whole kilograms.</param>
/// <param name="PreferredIntensity">
/// How intense a ride this person likes, in <c>[0.1, 1]</c> on the same scale as a
/// gondola's felt-G intensity.
/// </param>
/// <param name="Happiness">
/// The person's <em>current</em> happiness in <c>[0, 1]</c> (1 is very happy), already
/// reduced for the time they have spent waiting past the grumpiness onset.
/// </param>
/// <param name="Nausea">The person's nausea rating in <c>[0, 1]</c> (0 is not nauseous).</param>
/// <remarks>
/// Shared by several of the module's features and by
/// <see cref="IRideQueueService"/>, so it sits directly under
/// <c>DataTransferObjects</c> rather than inside one feature's namespace.
/// </remarks>
public sealed record PersonDto(
    long Number,
    string Name,
    int WeightInKilograms,
    double PreferredIntensity,
    double Happiness,
    double Nausea);
