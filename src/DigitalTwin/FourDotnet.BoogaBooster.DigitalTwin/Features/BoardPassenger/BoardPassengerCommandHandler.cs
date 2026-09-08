using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Features.BoardPassenger;

/// <summary>Boards a passenger through the ride store, validating the weight.</summary>
public sealed class BoardPassengerCommandHandler : CommandHandler<BoardPassengerCommand>
{
    private readonly IRideStore _store;

    public BoardPassengerCommandHandler(IRideStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task ExecuteAsync(BoardPassengerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var weight = command.WeightKg is { } kilograms ? new PassengerWeight(kilograms) : null;
        _store.BoardPassenger(command.HubIndex, command.GondolaIndex, command.Seat, weight);
        return Task.CompletedTask;
    }
}
