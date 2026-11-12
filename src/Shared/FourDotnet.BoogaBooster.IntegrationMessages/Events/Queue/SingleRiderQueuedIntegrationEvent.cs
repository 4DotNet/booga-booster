namespace FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;

/// <summary>
/// Published by the Queue module whenever an individual guest joins a ride's
/// single-riders line. Carries the ride, the person's unique number and the moment
/// they joined, so downstream modules can react to lone arrivals as they already can
/// to group arrivals (<see cref="GroupQueuedIntegrationEvent"/>).
/// </summary>
[TopicName("single-rider-queued")]
public sealed record SingleRiderQueuedIntegrationEvent(
    Guid RideId,
    long PersonNumber,
    DateTimeOffset QueuedAt) : IIntegrationEvent;
