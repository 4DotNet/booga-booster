using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Drives the ride simulation at the fixed physics timestep
/// (<see cref="RideParameters.TimeStep"/>). Timing runs off an injected
/// <see cref="TimeProvider"/> so the loop is deterministic under a fake time
/// provider in tests. Each tick advances the store by exactly one <c>dt</c>, keeping
/// the physics rate independent of any render/telemetry rate.
/// </summary>
public sealed class RideSimulationService : BackgroundService
{
    private readonly IRideStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RideSimulationService> _logger;

    public RideSimulationService(
        IRideStore store,
        TimeProvider timeProvider,
        ILogger<RideSimulationService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
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
                Tick();
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
    /// testing of the loop without driving the timer.
    /// </summary>
    internal void Tick() => _store.Advance(RideParameters.TimeStep);
}
