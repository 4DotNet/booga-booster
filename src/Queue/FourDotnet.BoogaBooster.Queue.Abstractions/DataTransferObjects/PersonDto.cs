namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;

/// <summary>
/// A single person waiting in a ride's queue: their unique number, name, weight in
/// kilograms and their experience on a 0–100 scale.
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
/// The person's happiness at the moment the DTO was produced, including any decay
/// from waiting in the queue: 0 is very sad, 100 is extremely happy.
/// </param>
/// <param name="PreferredRideIntensity">
/// The ride intensity the person enjoys most: 0 is very tame, 100 is the maximum
/// allowed G-force.
/// </param>
/// <param name="Nausea">How nauseous the person is: 0 is fine, 100 is extremely nauseous.</param>
public sealed record PersonDto(
    long Number,
    string Name,
    int WeightInKilograms,
    double Happiness,
    double PreferredRideIntensity,
    double Nausea);
