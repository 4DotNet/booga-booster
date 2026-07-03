namespace FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;

/// <summary>
/// Published by the Queue module whenever a group of guests joins a ride's
/// waiting line. Carries the group's identity and the number of people in it so
/// downstream modules can react to arrivals.
/// </summary>
[TopicName("group-queued")]
public sealed record GroupQueuedIntegrationEvent(
    Guid RideId,
    Guid GroupId,
    int PeopleCount,
    DateTimeOffset QueuedAt) : IIntegrationEvent;
