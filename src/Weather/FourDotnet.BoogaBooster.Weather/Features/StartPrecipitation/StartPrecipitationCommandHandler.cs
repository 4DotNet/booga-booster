using System.Diagnostics;
using System.Diagnostics.Metrics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Observability;

namespace FourDotnet.BoogaBooster.Weather.Features.StartPrecipitation;

/// <summary>
/// Starts a precipitation event on the world weather and publishes the resulting
/// weather update.
/// </summary>
public sealed class StartPrecipitationCommandHandler : CommandHandler<StartPrecipitationCommand>
{
    private readonly IWeatherStore _store;
    private readonly IWeatherUpdatePublisher _publisher;

    public StartPrecipitationCommandHandler(IWeatherStore store, IWeatherUpdatePublisher publisher)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    protected override async Task ExecuteAsync(StartPrecipitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = _store.StartPrecipitation(command.Type);

        BoogaBoosterTelemetry.WeatherDisturbances.Add(
            1,
            new TagList
            {
                {
                    WeatherTelemetryAttributes.DisturbanceKind,
                    WeatherTelemetryAttributes.PrecipitationDisturbance
                },
            });

        await _publisher.PublishAsync(result.Snapshot, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records the precipitation asked for and the regime the command found, so a span
    /// shows both the request and what it interrupted.
    /// </summary>
    protected override void EnrichActivity(Activity activity, StartPrecipitationCommand command)
    {
        activity.SetTag(WeatherTelemetryAttributes.PrecipitationType, command.Type.ToString());
        activity.SetTag(WeatherTelemetryAttributes.Regime, _store.GetSnapshot().Regime.ToString());
    }
}
