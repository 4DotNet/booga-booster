namespace FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;

/// <summary>
/// Published by the Queue module whenever an individual guest joins a ride's
/// single-riders line. Mirrors <see cref="GroupQueuedIntegrationEvent"/> so lone
/// arrivals can be reacted to as group arrivals already can.
/// </summary>
[TopicName("single-rider-queued")]
public sealed record SingleRiderQueuedIntegrationEvent(
    Guid RideId,
    long PersonNumber,
    DateTimeOffset QueuedAt) : IIntegrationEvent;
