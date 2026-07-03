namespace FourDotnet.BoogaBooster.Queue.Abstractions;

/// <summary>
/// Public contract of the Queue module. Adds arriving groups to a ride's waiting
/// line and reports the current line state. Enqueuing a group publishes a
/// <c>GroupQueuedIntegrationEvent</c> as a side effect.
/// </summary>
public interface IRideQueueService
{
    /// <summary>
    /// Adds a group of <paramref name="groupSize"/> guests to the back of the
    /// given ride's queue, publishes the group-queued integration event, and
    /// returns the enqueued group.
    /// </summary>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="groupSize"/> is less than one.
    /// </exception>
    Task<QueuedGroupDto> EnqueueGroupAsync(Guid rideId, int groupSize, CancellationToken cancellationToken);

    /// <summary>Returns a snapshot of the given ride's waiting line.</summary>
    QueueStatusDto GetStatus(Guid rideId);
}
