using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
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

    private async Task FillRideAsync(Guid rideId, CancellationToken cancellationToken)
    {
        var baseCount = ArrivalPlanner.PlanArrivalCount(
            _options.MinArrivalsPerCycle,
            _options.MaxArrivalsPerCycle,
            _rng);

        // Track the crowd to the weather: scale the planned headcount by the latest
        // NiceWeather indicator before partitioning into groups. In the worst
        // weather this floors to zero and nobody arrives this cycle.
        var totalPeople = ArrivalPlanner.ScaleForWeather(
            baseCount,
            _weatherInfluence.Current,
            _options);

        if (totalPeople == 0)
        {
            return;
        }

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

            await _queueService.EnqueueGroupAsync(rideId, size, cancellationToken);
        }
    }
}
