using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;

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

        // Counted only once the domain has accepted the boarding, and untagged: seat
        // and gondola would multiply the series thirty-two-fold (design D6).
        BoogaBoosterTelemetry.PassengersBoarded.Add(1);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Records the seat addressed and whether a weight was supplied — deliberately
    /// not the weight itself, and never a passenger's name. Weight is a continuous
    /// value that says which body sat down; whether one was supplied is what a
    /// diagnosis actually needs (a random weight was drawn, or the caller gave one).
    /// </summary>
    protected override void EnrichActivity(Activity activity, BoardPassengerCommand command)
    {
        activity.SetTag(RideTelemetryAttributes.HubIndex, command.HubIndex);
        activity.SetTag(RideTelemetryAttributes.GondolaIndex, command.GondolaIndex);
        activity.SetTag(RideTelemetryAttributes.Seat, command.Seat.ToString());
        activity.SetTag(RideTelemetryAttributes.WeightSupplied, command.WeightKg is not null);
    }
}
