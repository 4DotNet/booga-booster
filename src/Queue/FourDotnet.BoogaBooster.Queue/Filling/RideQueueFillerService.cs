using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using FourDotnet.BoogaBooster.Queue.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FourDotnet.BoogaBooster.Queue.Filling;

/// <summary>
/// Hosted background service that continuously fills each configured ride's queue
/// with arriving guests — a mix of lone guests and groups — four to eight at a time
/// (configurable). The wait between cycles is re-drawn after every cycle from a
/// weather-dependent band: ten to thirty seconds in fair weather, thirty to sixty
/// once the weather turns bad. Timing runs off an injected
/// <see cref="TimeProvider"/> so the loop is deterministic under test, and arrivals
/// stop while a queue is at capacity.
/// </summary>
internal sealed class RideQueueFillerService : BackgroundService
{
    private const string FillOperationName = "FillRideQueue";

    /// <summary>
    /// Offset applied to <see cref="QueueModuleOptions.RandomSeed"/> to seed the
    /// single-riders generator, so the two streams are reproducible but not identical.
    /// </summary>
    private const int SinglesSeedOffset = 1;

    private readonly IRideQueueService _queueService;
    private readonly IRideQueueStore _store;
    private readonly IWeatherInfluence _weatherInfluence;
    private readonly QueueModuleOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RideQueueFillerService> _logger;
    private readonly Random _rng;
    private readonly Random _singlesRng;

    public RideQueueFillerService(
        IRideQueueService queueService,
        IRideQueueStore store,
        IWeatherInfluence weatherInfluence,
        IOptions<QueueModuleOptions> options,
        TimeProvider timeProvider,
        ILogger<RideQueueFillerService> logger)
    {
        _queueService = queueService;
        _store = store;
        _weatherInfluence = weatherInfluence;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _rng = _options.RandomSeed is { } seed ? new Random(seed) : new Random();

        // The single-riders stream draws from its own generator so it never shifts the
        // group stream's (or the interval's) draws: enabling it leaves every group
        // arrival exactly as it would have been. Derived from the same seed, so a
        // seeded run stays fully reproducible.
        _singlesRng = _options.RandomSeed is { } singlesSeed
            ? new Random(unchecked(singlesSeed + SinglesSeedOffset))
            : new Random();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = PlanNextInterval();

        _logger.LogInformation(
            "Ride queue filler started for {RideCount} ride(s); first cycle in {Interval}.",
            _options.RideIds.Count,
            interval);

        using var timer = new PeriodicTimer(interval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunFillCycleAsync(stoppingToken);

                // Re-draw against the weather as it stands right now: a downturn
                // stretches the wait before the next batch of guests turns up.
                timer.Period = PlanNextInterval();
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    /// <summary>
    /// Draws the wait before the next fill cycle from the interval band matching the
    /// current weather. Exposed for deterministic testing of the pacing without
    /// driving the timer.
    /// </summary>
    internal TimeSpan PlanNextInterval() =>
        IntervalPlanner.PlanFillInterval(_weatherInfluence.Current, _options, _rng);

    /// <summary>
    /// Runs a single fill cycle across every configured ride. Exposed for
    /// deterministic testing of the fill behaviour without driving the timer.
    /// </summary>
    internal async Task RunFillCycleAsync(CancellationToken cancellationToken)
    {
        foreach (var rideId in _options.RideIds)
        {
            await FillRideAsync(rideId, cancellationToken);
        }
    }

    /// <summary>
    /// Fills one ride's line. The pass acts every ten seconds at most, so it is worth
    /// a span of its own (ADR-0009) — including the passes that add nobody, which is
    /// the answer to "why is the line not growing".
    /// </summary>
    private async Task FillRideAsync(Guid rideId, CancellationToken cancellationToken)
    {
        using var activity = BoogaBoosterTelemetry.ActivitySource.StartActivity(FillOperationName);
        activity?.SetTag(QueueTelemetryAttributes.RideId, rideId);

        var groupsAdded = 0;
        var peopleAdded = 0;
        var singleRidersAdded = 0;

        try
        {
            // One weather read per cycle drives both arrival streams.
            var niceWeather = _weatherInfluence.Current;

            (groupsAdded, peopleAdded) = await FillGroupsAsync(rideId, niceWeather, cancellationToken);

            // An additional stream, planned after the groups and sharing no budget
            // with them: lone guests never reduce the number arriving in groups.
            singleRidersAdded = await FillSingleRidersAsync(rideId, niceWeather, cancellationToken);
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
        finally
        {
            if (activity is not null)
            {
                activity.SetTag(QueueTelemetryAttributes.GroupsAdded, groupsAdded);
                activity.SetTag(QueueTelemetryAttributes.PeopleAdded, peopleAdded);
                activity.SetTag(QueueTelemetryAttributes.SingleRidersAdded, singleRidersAdded);
            }
        }
    }

    /// <summary>
    /// Plans this cycle's group arrivals and enqueues them until the regular queue is
    /// full. Unchanged from before the single-riders stream existed.
    /// </summary>
    /// <returns>The groups and people actually added to the line.</returns>
    private async Task<(int GroupsAdded, int PeopleAdded)> FillGroupsAsync(
        Guid rideId,
        double niceWeather,
        CancellationToken cancellationToken)
    {
        var baseCount = ArrivalPlanner.PlanArrivalCount(
            _options.MinArrivalsPerCycle,
            _options.MaxArrivalsPerCycle,
            _rng);

        // Track the crowd to the weather: scale the planned headcount by the latest
        // NiceWeather indicator before partitioning into groups. In the worst
        // weather this floors to zero and nobody arrives this cycle.
        var totalPeople = ArrivalPlanner.ScaleForWeather(baseCount, niceWeather, _options);
        if (totalPeople == 0)
        {
            return (0, 0);
        }

        var groupSizes = ArrivalPlanner.PlanGroupSizes(
            totalPeople,
            _options.MinGroupSize,
            _options.MaxGroupSize,
            _rng);

        var queue = _store.GetOrCreate(rideId);
        var groupsAdded = 0;
        var peopleAdded = 0;

        foreach (var size in groupSizes)
        {
            if (!queue.CanAccept(size))
            {
                _logger.LogDebug(
                    "Ride {RideId} queue is full ({PeopleWaiting}/{Max}); skipping remaining arrivals.",
                    rideId,
                    queue.PeopleWaiting,
                    queue.MaxPeople);
                break;
            }

            var enqueued = await _queueService.EnqueueGroupAsync(rideId, size, cancellationToken);

            // A party too large to board in one pass joins as several groups, so
            // count what actually went into the line rather than what was planned.
            groupsAdded += enqueued.Count;
            peopleAdded += enqueued.Sum(group => group.Size);
        }

        return (groupsAdded, peopleAdded);
    }

    /// <summary>
    /// Plans this cycle's single-rider arrivals — scaled by the same weather reading
    /// as the groups — and enqueues them one at a time until the single-riders line
    /// is full; any remainder of the cycle's lone guests is dropped.
    /// </summary>
    /// <returns>The number of individuals actually added to the single-riders line.</returns>
    private async Task<int> FillSingleRidersAsync(
        Guid rideId,
        double niceWeather,
        CancellationToken cancellationToken)
    {
        var baseCount = ArrivalPlanner.PlanArrivalCount(
            _options.MinSingleArrivalsPerCycle,
            _options.MaxSingleArrivalsPerCycle,
            _singlesRng);

        var arrivals = ArrivalPlanner.ScaleForWeather(baseCount, niceWeather, _options);
        if (arrivals == 0)
        {
            return 0;
        }

        var singles = _store.GetOrCreateSingles(rideId);
        var added = 0;

        for (var i = 0; i < arrivals; i++)
        {
            if (!singles.CanAccept(1))
            {
                _logger.LogDebug(
                    "Ride {RideId} single-riders line is full ({PeopleWaiting}/{Max}); skipping remaining single riders.",
                    rideId,
                    singles.PeopleWaiting,
                    singles.MaxPeople);
                break;
            }

            // The check above is only a fast path: a concurrent requeue can refill the
            // line in between, so the atomic admission's answer is the one that counts.
            if (await _queueService.EnqueueSingleRiderAsync(rideId, cancellationToken) is null)
            {
                break;
            }

            added++;
        }

        return added;
    }
}
