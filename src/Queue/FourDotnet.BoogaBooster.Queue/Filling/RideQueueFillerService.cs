using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FourDotnet.BoogaBooster.Queue.Filling;

/// <summary>
/// Hosted background service that continuously fills each configured ride's queue
/// with arriving guests — a mix of lone guests and groups — at a rate of roughly
/// four to eight people per minute (both configurable). Timing runs off an
/// injected <see cref="TimeProvider"/> so the loop is deterministic under test,
/// and arrivals stop while a queue is at capacity.
/// </summary>
internal sealed class RideQueueFillerService : BackgroundService
{
    private readonly IRideQueueService _queueService;
    private readonly IRideQueueStore _store;
    private readonly QueueModuleOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RideQueueFillerService> _logger;
    private readonly Random _rng;

    public RideQueueFillerService(
        IRideQueueService queueService,
        IRideQueueStore store,
        IOptions<QueueModuleOptions> options,
        TimeProvider timeProvider,
        ILogger<RideQueueFillerService> logger)
    {
        _queueService = queueService;
        _store = store;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _rng = _options.RandomSeed is { } seed ? new Random(seed) : new Random();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Ride queue filler started for {RideCount} ride(s), every {Interval}.",
            _options.RideIds.Count,
            _options.FillInterval);

        using var timer = new PeriodicTimer(_options.FillInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var rideId in _options.RideIds)
                {
                    await FillRideAsync(rideId, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }

    private async Task FillRideAsync(Guid rideId, CancellationToken cancellationToken)
    {
        var totalPeople = ArrivalPlanner.PlanArrivalCount(
            _options.MinArrivalsPerCycle,
            _options.MaxArrivalsPerCycle,
            _rng);

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
