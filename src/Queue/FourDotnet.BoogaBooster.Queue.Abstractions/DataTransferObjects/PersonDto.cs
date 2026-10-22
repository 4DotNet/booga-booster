namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;

/// <summary>
/// A single person waiting in a ride's queue: their unique number, name, weight in
/// kilograms, and their mood as it stands at the moment the DTO was built.
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
/// Current happiness (0–100), after any decay from waiting in the queue.
/// </param>
/// <param name="PreferredRideIntensity">How intense the person likes their ride (0–100).</param>
/// <param name="Nausea">How nauseous the person is (0–100).</param>
public sealed record PersonDto(
    long Number,
    string Name,
    int WeightInKilograms,
    double Happiness,
    double PreferredRideIntensity,
    double Nausea);
