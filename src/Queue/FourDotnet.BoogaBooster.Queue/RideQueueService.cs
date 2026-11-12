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
/// for every group that joins a line; individuals join the ride's separate
/// single-riders line and are announced with a
/// <see cref="SingleRiderQueuedIntegrationEvent"/>.
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
        // Either line may not exist yet — a ride nobody has joined reports both empty.
        // Each line is snapshotted once, and its count derived from that snapshot, so
        // a count can never disagree with the list beside it.
        QueuedGroupDto[] groups = _store.Find(rideId) is { } queue
            ? [.. queue.SnapshotGroups().Select(ToDto)]
            : [];

        PersonDto[] singleRiders = _store.FindSingles(rideId) is { } singles
            ? ToDtos(singles.SnapshotPeople())
            : [];

        // PeopleWaiting keeps meaning "people waiting in groups": single riders are
        // their own figure and never inflate the group headcount.
        return new GetQueueStatusResponse(
            rideId,
            GroupCount: groups.Length,
            PeopleWaiting: groups.Sum(g => g.Size),
            Groups: groups,
            SingleRidersWaiting: singleRiders.Length,
            SingleRiders: singleRiders);
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

    public async Task<PersonDto> EnqueueSingleRiderAsync(Guid rideId, CancellationToken cancellationToken)
    {
        var singles = _store.GetOrCreateSingles(rideId);

        // The aggregate is the arbiter of its own capacity: a full line throws here,
        // before anything is logged or published.
        var person = _personGenerator.Next();
        singles.Enqueue(person);

        // The person's number only — never the name — so no personal data is logged.
        _logger.LogInformation(
            "Single rider {PersonNumber} ({Weight} kg) joined ride {RideId}'s single-riders line; " +
            "{SingleRidersWaiting} now waiting.",
            person.Number,
            person.WeightInKilograms,
            rideId,
            singles.PeopleWaiting);

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

        // A ride nobody has joined has no line yet; that is "nobody waiting", not an
        // error, and asking must not create the line as a side effect.
        var singles = _store.FindSingles(rideId);
        var taken = singles?.TakeUpTo(count);
        if (singles is null || taken is null || taken.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<PersonDto>>([]);
        }

        _logger.LogInformation(
            "{Count} single rider(s) left ride {RideId}'s single-riders line to board; " +
            "{SingleRidersWaiting} still waiting.",
            taken.Count,
            rideId,
            singles.PeopleWaiting);

        return Task.FromResult<IReadOnlyList<PersonDto>>(ToDtos(taken));
    }

    /// <remarks>
    /// Deliberately not cancellable: this is the compensation for a take whose riders
    /// could not be seated, and abandoning it half-way is exactly the lost guest it
    /// exists to prevent. The work is a single in-memory step, so there is nothing to
    /// wait for either.
    /// </remarks>
    public Task RequeueSingleRidersAsync(
        Guid rideId,
        IReadOnlyList<PersonDto> people,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(people);

        if (people.Count == 0)
        {
            return Task.CompletedTask;
        }

        var returning = new Person[people.Count];
        for (var i = 0; i < returning.Length; i++)
        {
            var person = people[i];
            returning[i] = new Person(person.Number, person.Name, person.WeightInKilograms);
        }

        var singles = _store.GetOrCreateSingles(rideId);
        singles.RequeueFront(returning);

        _logger.LogInformation(
            "{Count} single rider(s) returned to the front of ride {RideId}'s single-riders line; " +
            "{SingleRidersWaiting} now waiting.",
            returning.Length,
            rideId,
            singles.PeopleWaiting);

        return Task.CompletedTask;
    }

    private static QueuedGroupDto ToDto(QueuedGroup group)
    {
        var people = group.Members
            .Select(ToDto)
            .ToArray();

        return new QueuedGroupDto(group.GroupId, people);
    }

    private static PersonDto ToDto(Person person) =>
        new(person.Number, person.Name, person.WeightInKilograms);

    private static PersonDto[] ToDtos(IReadOnlyList<Person> people)
    {
        var dtos = new PersonDto[people.Count];
        for (var i = 0; i < dtos.Length; i++)
        {
            dtos[i] = ToDto(people[i]);
        }

        return dtos;
    }
}
