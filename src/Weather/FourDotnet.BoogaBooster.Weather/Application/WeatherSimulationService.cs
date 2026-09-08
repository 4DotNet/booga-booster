using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.Weather.Domain;
using FourDotnet.BoogaBooster.Weather.Observability;
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
    private const string AdvanceOperationName = "AdvanceWeather";

    /// <summary>Whether the advance actually moved the conditions.</summary>
    private const string WeatherChangedAttribute = "weather.changed";

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
    /// <remarks>
    /// The weather advances every few seconds, not every physics step, so each advance
    /// is worth a span of its own (ADR-0009) rather than a counter — including the
    /// advances that change nothing, which is itself the answer to "why is it still
    /// raining".
    /// </remarks>
    /// <returns><c>true</c> when an update was published.</returns>
    internal async Task<bool> TickAsync(CancellationToken cancellationToken)
    {
        using var activity = BoogaBoosterTelemetry.ActivitySource.StartActivity(AdvanceOperationName);

        try
        {
            var result = _store.Advance(WeatherDefaults.TickInterval);

            if (activity is not null)
            {
                activity.SetTag(WeatherTelemetryAttributes.Regime, result.Snapshot.Regime.ToString());
                activity.SetTag(WeatherTelemetryAttributes.NiceWeather, result.Snapshot.NiceWeather);
                activity.SetTag(WeatherChangedAttribute, result.Changed);
            }

            if (!result.Changed)
            {
                return false;
            }

            await _publisher.PublishAsync(result.Snapshot, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
    }
}
