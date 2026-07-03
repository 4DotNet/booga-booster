using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Weather.Application;

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

    public override async Task HandleAsync(StartPrecipitationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = _store.StartPrecipitation(command.Type);
        await _publisher.PublishAsync(result.Snapshot, cancellationToken).ConfigureAwait(false);
    }
}
