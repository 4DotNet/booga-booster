using FourDotnet.BoogaBooster.Queue.Domain;

namespace FourDotnet.BoogaBooster.Queue.Infrastructure;

/// <summary>
/// In-memory home for each ride's <see cref="RideQueue"/> aggregate and its
/// <see cref="SingleRiderQueue"/> aggregate. Module
/// internal — persistence choice is deferred, so a process-lifetime store is
/// sufficient for now.
/// </summary>
public interface IRideQueueStore
{
    /// <summary>
    /// Returns the ride's queue, creating an empty one on first access.
    /// </summary>
    RideQueue GetOrCreate(Guid rideId);

    /// <summary>Returns the ride's queue, or null when none exists yet.</summary>
    RideQueue? Find(Guid rideId);

    /// <summary>
    /// Returns the ride's single-riders line, creating an empty one on first access.
    /// It is held separately from the ride's group queue: creating or changing one
    /// never touches the other.
    /// </summary>
    SingleRiderQueue GetOrCreateSingles(Guid rideId);

    /// <summary>Returns the ride's single-riders line, or null when none exists yet.</summary>
    SingleRiderQueue? FindSingles(Guid rideId);
}
