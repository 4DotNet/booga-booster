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
/// (configurable) — and, as an additional stream, each ride's single-riders line
/// with one to three individuals per cycle. The wait between cycles is re-drawn after every cycle from a
/// weather-dependent band: ten to thirty seconds in fair weather, thirty to sixty
/// once the weather turns bad. Timing runs off an injected
/// <see cref="TimeProvider"/> so the loop is deterministic under test, and arrivals
/// stop while a queue is at capacity.
/// </summary>
internal sealed class RideQueueFillerService : BackgroundService
{
    private const string FillOperationName = "FillRideQueue";

    private readonly IRideQueueService _queueService;
    private readonly IRideQueueStore _store;
    private readonly IWeatherInfluence _weatherInfluence;
    private readonly QueueModuleOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RideQueueFillerService> _logger;
    private readonly Random _rng;

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
    /// Fills one ride's lines: the group arrivals first, then the single-riders
    /// stream. The pass acts every ten seconds at most, so it is worth a span of its
    /// own (ADR-0009) — including the passes that add nobody, which is the answer to
    /// "why is the line not growing".
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
            // One weather read for the whole cycle, so parties and lone guests are
            // scaled by the same reading even if an update lands mid-cycle.
            var niceWeather = _weatherInfluence.Current;

            var baseCount = ArrivalPlanner.PlanArrivalCount(
                _options.MinArrivalsPerCycle,
                _options.MaxArrivalsPerCycle,
                _rng);

            // Track the crowd to the weather: scale the planned headcount by the latest
            // NiceWeather indicator before partitioning into groups. In the worst
            // weather this floors to zero and no group arrives this cycle.
            var totalPeople = ArrivalPlanner.ScaleForWeather(
                baseCount,
                niceWeather,
                _options);

            if (totalPeople > 0)
            {
                var groupSizes = ArrivalPlanner.PlanGroupSizes(
                    totalPeople,
                    _options.MinGroupSize,
                    _options.MaxGroupSize,
                    _rng);

                var queue = _store.GetOrCreate(rideId);

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
            }

            // The single-riders stream is an additional arrival stream, planned and
            // enqueued only after the groups: it draws from the same seeded source and
            // the same weather reading, but shares no headcount budget with them, so
            // it never reduces how many people arrive in groups.
            var singleRiders = ArrivalPlanner.ScaleForWeather(
                ArrivalPlanner.PlanArrivalCount(
                    _options.MinSingleArrivalsPerCycle,
                    _options.MaxSingleArrivalsPerCycle,
                    _rng),
                niceWeather,
                _options);

            if (singleRiders > 0)
            {
                var singles = _store.GetOrCreateSingles(rideId);

                for (var i = 0; i < singleRiders; i++)
                {
                    if (!singles.CanAccept(1))
                    {
                        _logger.LogDebug(
                            "Ride {RideId} single-riders queue is full ({PeopleWaiting}/{Max}); " +
                            "skipping remaining single riders.",
                            rideId,
                            singles.PeopleWaiting,
                            singles.MaxPeople);
                        break;
                    }

                    await _queueService.EnqueueSingleRiderAsync(rideId, cancellationToken);
                    singleRidersAdded++;
                }
            }
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
}
