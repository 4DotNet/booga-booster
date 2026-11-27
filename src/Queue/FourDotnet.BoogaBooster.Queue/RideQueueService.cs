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
/// Default <see cref="IRideQueueService"/>. Enqueues arriving groups and single
/// riders onto the in-memory per-ride lines, publishing a
/// <see cref="GroupQueuedIntegrationEvent"/> for every group and a
/// <see cref="SingleRiderQueuedIntegrationEvent"/> for every single rider that joins.
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
        QueuedGroupDto[] groups = _store.Find(rideId) is { } queue
            ? [.. queue.SnapshotGroups().Select(ToDto)]
            : [];

        PersonDto[] singles = _store.FindSingles(rideId) is { } singlesLine
            ? [.. singlesLine.SnapshotPeople().Select(ToDto)]
            : [];

        // PeopleWaiting deliberately counts people in groups only: single riders are
        // their own figure and never inflate the group headcount the loading pass reads.
        return new GetQueueStatusResponse(
            rideId,
            GroupCount: groups.Length,
            PeopleWaiting: groups.Sum(g => g.Size),
            Groups: groups,
            SingleRidersWaiting: singles.Length,
            SingleRiders: singles);
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

    public async Task<PersonDto?> EnqueueSingleRiderAsync(Guid rideId, CancellationToken cancellationToken)
    {
        var singles = _store.GetOrCreateSingles(rideId);

        var person = _personGenerator.Next();
        if (!singles.TryEnqueue(person))
        {
            _logger.LogDebug(
                "Ride {RideId} single-riders line is full ({Max}); single rider not admitted.",
                rideId,
                singles.MaxPeople);
            return null;
        }

        _logger.LogInformation(
            "Single rider {PersonNumber} ({Weight} kg) joined ride {RideId}'s single-riders line; {SingleRidersWaiting} now waiting.",
            person.Number,
            person.WeightInKilograms,
            rideId,
            singles.PeopleWaiting);

        // Untagged, like the group counters: ride id and person number are unbounded.
        BoogaBoosterTelemetry.QueueSingleRidersQueued.Add(1);

        var integrationEvent = new SingleRiderQueuedIntegrationEvent(
            RideId: rideId,
            PersonNumber: person.Number,
            QueuedAt: _timeProvider.GetUtcNow());

        await _publisher.PublishAsync(integrationEvent, cancellationToken);

        return ToDto(person);
    }

    public Task<IReadOnlyList<PersonDto>> TakeSingleRidersAsync(
        Guid rideId,
        int count,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var singles = _store.FindSingles(rideId);
        var taken = singles?.TakeUpTo(count) ?? [];
        if (taken.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PersonDto>>([]);
        }

        _logger.LogInformation(
            "{Count} single rider(s) left ride {RideId}'s single-riders line to board; {SingleRidersWaiting} still waiting.",
            taken.Count,
            rideId,
            singles!.PeopleWaiting);

        return Task.FromResult<IReadOnlyList<PersonDto>>([.. taken.Select(ToDto)]);
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

        Person[] returning = [.. people.Select(p => new Person(p.Number, p.Name, p.WeightInKilograms))];
        _store.GetOrCreateSingles(rideId).RequeueFront(returning);

        _logger.LogWarning(
            "{Count} single rider(s) could not be seated on ride {RideId} and returned to the front of the line.",
            returning.Length,
            rideId);

        return Task.CompletedTask;
    }

    private static QueuedGroupDto ToDto(QueuedGroup group) =>
        new(group.GroupId, [.. group.Members.Select(ToDto)]);

    private static PersonDto ToDto(Person person) =>
        new(person.Number, person.Name, person.WeightInKilograms);
}
