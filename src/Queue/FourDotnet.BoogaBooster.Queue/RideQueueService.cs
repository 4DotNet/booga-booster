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
        var groups = queue.EnqueueAll(arrivals);

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
                QueuedAt: _timeProvider.GetUtcNow());

            await _publisher.PublishAsync(integrationEvent, cancellationToken);

            enqueued.Add(ToDto(group));
        }

        return enqueued;
    }

    public GetQueueStatusResponse GetStatus(Guid rideId)
    {
        var queue = _store.Find(rideId);
        QueuedGroupDto[] groups = queue is null
            ? []
            : [.. queue.SnapshotGroups().Select(ToDto)];

        var singles = _store.FindSingles(rideId);
        PersonDto[] singleRiders = singles is null
            ? []
            : [.. singles.SnapshotPeople().Select(ToDto)];

        return new GetQueueStatusResponse(
            rideId,
            GroupCount: groups.Length,
            PeopleWaiting: groups.Sum(g => g.Size),
            Groups: groups,
            SingleRidersWaiting: singleRiders.Length,
            SingleRiders: singleRiders);
    }

    public async Task<PersonDto> EnqueueSingleRiderAsync(Guid rideId, CancellationToken cancellationToken)
    {
        var line = _store.GetOrCreateSingles(rideId);
        var person = _personGenerator.Next();
        line.Enqueue(person);

        _logger.LogInformation(
            "Single rider {PersonNumber} ({Weight} kg) joined ride {RideId}; {SingleRidersWaiting} now waiting alone.",
            person.Number,
            person.WeightInKilograms,
            rideId,
            line.PeopleWaiting);

        await _publisher.PublishAsync(
            new SingleRiderQueuedIntegrationEvent(rideId, person.Number, _timeProvider.GetUtcNow()),
            cancellationToken);

        return ToDto(person);
    }

    public Task<IReadOnlyList<PersonDto>> TakeSingleRidersAsync(
        Guid rideId,
        int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var line = _store.FindSingles(rideId);
        if (line is null)
        {
            return Task.FromResult<IReadOnlyList<PersonDto>>([]);
        }

        var taken = line.TakeUpTo(count);
        if (taken.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PersonDto>>([]);
        }

        _logger.LogInformation(
            "{Count} single rider(s) left ride {RideId}'s line to board; {SingleRidersWaiting} still waiting.",
            taken.Count,
            rideId,
            line.PeopleWaiting);

        var dtos = new PersonDto[taken.Count];
        for (var i = 0; i < dtos.Length; i++)
        {
            dtos[i] = ToDto(taken[i]);
        }

        return Task.FromResult<IReadOnlyList<PersonDto>>(dtos);
    }

    public Task RequeueSingleRidersAsync(
        Guid rideId,
        IReadOnlyList<PersonDto> people,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(people);
        cancellationToken.ThrowIfCancellationRequested();

        if (people.Count == 0)
        {
            return Task.CompletedTask;
        }

        var restored = new Person[people.Count];
        for (var i = 0; i < restored.Length; i++)
        {
            restored[i] = new Person(people[i].Number, people[i].Name, people[i].WeightInKilograms);
        }

        _store.GetOrCreateSingles(rideId).RequeueFront(restored);

        _logger.LogInformation(
            "{Count} single rider(s) returned to the front of ride {RideId}'s line.",
            restored.Length,
            rideId);

        return Task.CompletedTask;
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

    private static PersonDto ToDto(Person person) =>
        new(person.Number, person.Name, person.WeightInKilograms);

    private static QueuedGroupDto ToDto(QueuedGroup group)
    {
        var people = group.Members
            .Select(ToDto)
            .ToArray();

        return new QueuedGroupDto(group.GroupId, people);
    }
}
