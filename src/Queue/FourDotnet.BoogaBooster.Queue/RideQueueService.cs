using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Filling;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Logging;

namespace FourDotnet.BoogaBooster.Queue;

/// <summary>
/// Default <see cref="IRideQueueService"/>. Enqueues arriving groups onto the
/// in-memory per-ride queues and publishes a <see cref="GroupQueuedIntegrationEvent"/>
/// for every group that joins a line.
/// </summary>
internal sealed class RideQueueService : IRideQueueService
{
    private readonly IRideQueueStore _store;
    private readonly IPersonGenerator _personGenerator;
    private readonly IIntegrationEventPublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RideQueueService> _logger;

    public RideQueueService(
        IRideQueueStore store,
        IPersonGenerator personGenerator,
        IIntegrationEventPublisher publisher,
        TimeProvider timeProvider,
        ILogger<RideQueueService> logger)
    {
        _store = store;
        _personGenerator = personGenerator;
        _publisher = publisher;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<QueuedGroupDto> EnqueueGroupAsync(
        Guid rideId,
        int groupSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(groupSize, 1);

        var queue = _store.GetOrCreate(rideId);
        var group = queue.Enqueue(_personGenerator.CreateGroup(groupSize));

        _logger.LogInformation(
            "Group {GroupId} of {Size} ({Weight} kg) joined ride {RideId}; {PeopleWaiting} now waiting.",
            group.GroupId,
            group.Size,
            group.TotalWeightInKilograms,
            rideId,
            queue.PeopleWaiting);

        var integrationEvent = new GroupQueuedIntegrationEvent(
            RideId: rideId,
            GroupId: group.GroupId,
            PeopleCount: group.Size,
            QueuedAt: _timeProvider.GetUtcNow());

        await _publisher.PublishAsync(integrationEvent, cancellationToken);

        return ToDto(group);
    }

    public QueueStatusDto GetStatus(Guid rideId)
    {
        var queue = _store.Find(rideId);
        if (queue is null)
        {
            return new QueueStatusDto(rideId, GroupCount: 0, PeopleWaiting: 0, Groups: []);
        }

        var groups = queue.SnapshotGroups()
            .Select(ToDto)
            .ToArray();

        return new QueueStatusDto(
            rideId,
            GroupCount: groups.Length,
            PeopleWaiting: groups.Sum(g => g.Size),
            Groups: groups);
    }

    public Task<QueuedGroupDto?> TakeGroupAsync(
        Guid rideId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var queue = _store.Find(rideId);
        var removed = queue?.Remove(groupId);
        if (removed is null)
        {
            return Task.FromResult<QueuedGroupDto?>(null);
        }

        _logger.LogInformation(
            "Group {GroupId} of {Size} left ride {RideId}'s queue to board; {PeopleWaiting} still waiting.",
            removed.GroupId,
            removed.Size,
            rideId,
            queue!.PeopleWaiting);

        return Task.FromResult<QueuedGroupDto?>(ToDto(removed));
    }

    private static QueuedGroupDto ToDto(QueuedGroup group)
    {
        var people = group.Members
            .Select(p => new PersonDto(p.Number, p.Name, p.WeightInKilograms))
            .ToArray();

        return new QueuedGroupDto(group.GroupId, people);
    }
}
