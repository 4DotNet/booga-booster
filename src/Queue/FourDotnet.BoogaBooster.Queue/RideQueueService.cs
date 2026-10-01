using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
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

    public async Task<IReadOnlyList<QueuedGroupDto>> EnqueueGroupAsync(
        Guid rideId,
        int groupSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(groupSize, 1);

        var queue = _store.GetOrCreate(rideId);

        // A party too large to be seated in one boarding pass can never satisfy the
        // fully-fits rule, so it joins the line as several adjacent boardable groups.
        // Anything within capacity yields a single size and is never broken up.
        var sizes = GroupArrival.PartitionSizes(groupSize, queue.MaxBoardableGroupSize);
        if (sizes.Count > 1)
        {
            _logger.LogInformation(
                "Arrival of {Size} exceeds ride {RideId}'s boardable group size of {MaxBoardableGroupSize}; " +
                "splitting into {GroupCount} groups.",
                groupSize,
                rideId,
                queue.MaxBoardableGroupSize,
                sizes.Count);
        }

        // Admit the whole party in one step so a split arrival is never half-admitted
        // when the line is nearly full.
        var arrivals = sizes.Select(_personGenerator.CreateGroup).ToArray();
        var queuedAt = _timeProvider.GetUtcNow();
        var groups = queue.EnqueueAll(arrivals, queuedAt);

        var enqueued = new List<QueuedGroupDto>(groups.Count);

        foreach (var group in groups)
        {
            _logger.LogInformation(
                "Group {GroupId} of {Size} ({Weight} kg) joined ride {RideId}; {PeopleWaiting} now waiting.",
                group.GroupId,
                group.Size,
                group.TotalWeightInKilograms,
                rideId,
                queue.PeopleWaiting);

            // Untagged: ride id and group id are unbounded, and both are already on
            // the span this runs under (design D6).
            BoogaBoosterTelemetry.QueueGroupsQueued.Add(1);
            BoogaBoosterTelemetry.QueuePeopleQueued.Add(group.Size);

            var integrationEvent = new GroupQueuedIntegrationEvent(
                RideId: rideId,
                GroupId: group.GroupId,
                PeopleCount: group.Size,
                QueuedAt: group.QueuedAt);

            await _publisher.PublishAsync(integrationEvent, cancellationToken);

            enqueued.Add(ToDto(group, queuedAt));
        }

        return enqueued;
    }

    public GetQueueStatusResponse GetStatus(Guid rideId)
    {
        var queue = _store.Find(rideId);
        if (queue is null)
        {
            return new GetQueueStatusResponse(rideId, GroupCount: 0, PeopleWaiting: 0, Groups: [], AverageHappiness: null);
        }

        // One clock read for the whole snapshot, so every person's waited happiness and
        // the average describe the same instant.
        var now = _timeProvider.GetUtcNow();
        var snapshot = queue.SnapshotGroups();
        var groups = new QueuedGroupDto[snapshot.Count];
        var peopleWaiting = 0;
        var happinessSum = 0d;

        for (var i = 0; i < snapshot.Count; i++)
        {
            var group = ToDto(snapshot[i], now);
            groups[i] = group;

            var people = group.People;
            for (var p = 0; p < people.Count; p++)
            {
                happinessSum += people[p].Happiness;
            }

            peopleWaiting += group.Size;
        }

        return new GetQueueStatusResponse(
            rideId,
            GroupCount: groups.Length,
            PeopleWaiting: peopleWaiting,
            Groups: groups,
            AverageHappiness: peopleWaiting == 0 ? null : happinessSum / peopleWaiting);
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

        // The happiness that boards is the happiness eroded up to the moment of taking.
        return Task.FromResult<QueuedGroupDto?>(ToDto(removed, _timeProvider.GetUtcNow()));
    }

    /// <summary>
    /// Projects a group onto its DTO as of <paramref name="now"/>: each member's
    /// happiness is their arrival happiness eroded by the group's waiting time.
    /// </summary>
    private static QueuedGroupDto ToDto(QueuedGroup group, DateTimeOffset now)
    {
        var waited = group.WaitedAt(now);
        var members = group.Members;
        var people = new PersonDto[members.Count];

        for (var i = 0; i < members.Count; i++)
        {
            var person = members[i];
            people[i] = new PersonDto(
                person.Number,
                person.Name,
                person.WeightInKilograms,
                QueuePatience.HappinessAfter(person.Happiness, waited),
                person.PreferredIntensity,
                person.Nausea);
        }

        return new QueuedGroupDto(group.GroupId, people);
    }
}
