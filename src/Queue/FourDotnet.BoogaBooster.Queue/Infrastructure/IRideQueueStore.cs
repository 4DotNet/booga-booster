using FourDotnet.BoogaBooster.Queue.Domain;

namespace FourDotnet.BoogaBooster.Queue.Infrastructure;

/// <summary>
/// In-memory home for each ride's <see cref="RideQueue"/> aggregate. Module
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
}
