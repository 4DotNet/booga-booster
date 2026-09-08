using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Queue.Filling;

namespace FourDotnet.BoogaBooster.Queue.Features.RecordWeatherUpdate;

/// <summary>
/// Writes the observed weather into the shared <see cref="IWeatherInfluence"/>
/// state. Deliberately free of queue-domain logic — the filler applies the value
/// on its own schedule — and newest-wins, so out-of-order events settle on the
/// last one received.
/// </summary>
public sealed class RecordWeatherUpdateCommandHandler : CommandHandler<RecordWeatherUpdateCommand>
{
    private readonly IWeatherInfluence _weatherInfluence;

    public RecordWeatherUpdateCommandHandler(IWeatherInfluence weatherInfluence)
    {
        _weatherInfluence = weatherInfluence ?? throw new ArgumentNullException(nameof(weatherInfluence));
    }

    public override Task HandleAsync(RecordWeatherUpdateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _weatherInfluence.Update(command.NiceWeather);
        return Task.CompletedTask;
    }
}
