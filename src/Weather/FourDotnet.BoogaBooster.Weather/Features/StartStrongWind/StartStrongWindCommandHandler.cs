using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Weather.Application;

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
        await _publisher.PublishAsync(result.Snapshot, cancellationToken).ConfigureAwait(false);
    }
}
