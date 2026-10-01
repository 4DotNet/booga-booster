namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;

/// <summary>
/// A single person waiting in a ride's queue: their unique number, name, weight in
/// kilograms, and their three experience ratings on the 0–100 scale.
/// </summary>
/// <remarks>
/// Shared by several of the module's features and by
/// <see cref="IRideQueueService"/>, so it sits directly under
/// <c>DataTransferObjects</c> rather than inside one feature's namespace.
/// </remarks>
/// <param name="Number">The person's process-unique number.</param>
/// <param name="Name">The person's full name.</param>
/// <param name="WeightInKilograms">The person's weight in whole kilograms.</param>
/// <param name="Happiness">
/// The person's <em>current</em> happiness — their arrival happiness eroded by the time
/// their group has waited, as of the moment this DTO was produced.
/// </param>
/// <param name="PreferredIntensity">The ride intensity the person prefers.</param>
/// <param name="Nausea">The person's nausea.</param>
public sealed record PersonDto(
    long Number,
    string Name,
    int WeightInKilograms,
    double Happiness,
    double PreferredIntensity,
    double Nausea);
