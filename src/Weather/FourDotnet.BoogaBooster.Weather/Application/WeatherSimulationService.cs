using FourDotnet.BoogaBooster.Weather.Domain;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FourDotnet.BoogaBooster.Weather.Application;

/// <summary>
/// Drives the weather simulation. Ticks on a fixed interval sourced from the
/// injected <see cref="TimeProvider"/> (so the loop is deterministic under a fake
/// time provider in tests), advances the store each tick, and publishes a weather
/// update whenever a tick actually changes the conditions.
/// </summary>
public sealed class WeatherSimulationService : BackgroundService
{
    private readonly IWeatherStore _store;
    private readonly IWeatherUpdatePublisher _publisher;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<WeatherSimulationService> _logger;

    public WeatherSimulationService(
        IWeatherStore store,
        IWeatherUpdatePublisher publisher,
        TimeProvider timeProvider,
        ILogger<WeatherSimulationService> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(WeatherDefaults.TickInterval, _timeProvider);

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
            _logger.LogError(exception, "The weather simulation loop stopped unexpectedly.");
        }
    }

    /// <summary>
    /// Advances the simulation one tick and publishes a weather update only when
    /// the conditions actually changed. Exposed for deterministic testing of the
    /// publish gate without driving the timer.
    /// </summary>
    /// <returns><c>true</c> when an update was published.</returns>
    internal async Task<bool> TickAsync(CancellationToken cancellationToken)
    {
        var result = _store.Advance(WeatherDefaults.TickInterval);
        if (!result.Changed)
        {
            return false;
        }

        await _publisher.PublishAsync(result.Snapshot, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
