namespace FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;

/// <summary>
/// A single person waiting in a ride's queue: their unique guest number, name,
/// weight in kilograms and current mood.
/// </summary>
/// <param name="Number">The guest's process-unique number — their identity from queue to seat to exit.</param>
/// <param name="Name">The guest's full name.</param>
/// <param name="WeightInKilograms">The guest's weight in whole kilograms.</param>
/// <param name="Happiness">
/// The guest's current happiness in [0, 100], with the queue-wait decay already
/// applied as of the moment this DTO was produced.
/// </param>
/// <param name="PreferredG">The felt G-force (in g) the guest considers fun.</param>
/// <param name="Nausea">The guest's nausea in [0, 100].</param>
/// <remarks>
/// Shared by several of the module's features and by
/// <see cref="IRideQueueService"/>, so it sits directly under
/// <c>DataTransferObjects</c> rather than inside one feature's namespace.
/// </remarks>
public sealed record PersonDto(
    long Number,
    string Name,
    int WeightInKilograms,
    double Happiness,
    double PreferredG,
    double Nausea);
