using System.Diagnostics;
using System.Diagnostics.Metrics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Observability;

namespace FourDotnet.BoogaBooster.Weather.Features.StartStrongWind;

/// <summary>
/// Starts a strong-wind event on the world weather and publishes the resulting
/// weather update.
/// </summary>
public sealed class StartStrongWindCommandHandler : CommandHandler<StartStrongWindCommand>
{
    private readonly IWeatherStore _store;
    private readonly IWeatherUpdatePublisher _publisher;

    public StartStrongWindCommandHandler(IWeatherStore store, IWeatherUpdatePublisher publisher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    protected override async Task ExecuteAsync(StartStrongWindCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = _store.StartStrongWind();

        BoogaBoosterTelemetry.WeatherDisturbances.Add(
            1,
            new TagList
            {
                {
                    WeatherTelemetryAttributes.DisturbanceKind,
                    WeatherTelemetryAttributes.StrongWindDisturbance
                },
            });

        await _publisher.PublishAsync(result.Snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The command carries no payload, so the span records the conditions it found —
    /// the regime and the wind already blowing. That is what makes one invocation
    /// distinguishable from the next.
    /// </summary>
    protected override void EnrichActivity(Activity activity, StartStrongWindCommand command)
    {
        var snapshot = _store.GetSnapshot();
        activity.SetTag(WeatherTelemetryAttributes.Regime, snapshot.Regime.ToString());
        activity.SetTag(WeatherTelemetryAttributes.WindBeaufort, snapshot.WindBeaufort);
    }
}
