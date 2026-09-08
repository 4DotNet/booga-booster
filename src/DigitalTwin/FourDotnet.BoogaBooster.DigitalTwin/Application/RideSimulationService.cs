using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Drives the ride simulation at the fixed physics timestep
/// (<see cref="RideParameters.TimeStep"/>). Timing runs off an injected
/// <see cref="TimeProvider"/> so the loop is deterministic under a fake time
/// provider in tests. Each tick advances the store by exactly one <c>dt</c> and then
/// runs a loading pass, so the ride boards waiting (and newly-arrived) groups while
/// it is loading. The physics rate stays independent of any render/telemetry rate.
/// </summary>
public sealed class RideSimulationService : BackgroundService
{
    private readonly IRideStore _store;
    private readonly RideLoadingCoordinator _loadingCoordinator;
    private readonly Guid _rideId;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RideSimulationService> _logger;

    public RideSimulationService(
        IRideStore store,
        RideLoadingCoordinator loadingCoordinator,
        IOptions<DigitalTwinModuleOptions> options,
        TimeProvider timeProvider,
        ILogger<RideSimulationService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _loadingCoordinator = loadingCoordinator ?? throw new ArgumentNullException(nameof(loadingCoordinator));
        _rideId = options.Value.RideId;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(RideParameters.TimeStep, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await TickAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The ride simulation loop stopped unexpectedly.");
        }
    }

    /// <summary>
    /// Advances the simulation exactly one fixed step. Exposed for deterministic
    /// testing of the physics loop without driving the timer.
    /// </summary>
    /// <remarks>
    /// Measured, never spanned. At 120 Hz a span per tick would be 7,200 near-identical
    /// spans a minute — enough to swamp a trace backend and bury the one tick worth
    /// looking at, which the histogram surfaces instead (design D4).
    /// <para>
    /// Determinism is unaffected: the elapsed time is read <em>after</em> the advance
    /// and only handed to the histogram, so no clock reading ever enters the tick. The
    /// step stays exactly <see cref="RideParameters.TimeStep"/> — the pure function of
    /// state at fixed <c>dt</c> that <c>docs/01 §1.1</c> requires.
    /// </para>
    /// </remarks>
    internal void Tick()
    {
        var startedAt = Stopwatch.GetTimestamp();

        _store.Advance(RideParameters.TimeStep);

        BoogaBoosterTelemetry.SimulationTicks.Add(1);
        BoogaBoosterTelemetry.SimulationTickDuration.Record(
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
    }

    /// <summary>
    /// Advances one fixed step and then runs a loading pass. A failure in the loading
    /// pass is logged and swallowed so a transient boarding hiccup never tears down
    /// the simulation loop. Exposed for deterministic testing without the timer.
    /// </summary>
    internal async Task TickAsync(CancellationToken cancellationToken)
    {
        Tick();

        try
        {
            await _loadingCoordinator.RunLoadingPassAsync(_rideId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The loading pass failed for ride {RideId}.", _rideId);
        }
    }
}
