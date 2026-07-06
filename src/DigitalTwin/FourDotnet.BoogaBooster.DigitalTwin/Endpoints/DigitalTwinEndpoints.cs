using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Features.BoardPassenger;
using FourDotnet.BoogaBooster.DigitalTwin.Features.GetRideTelemetry;
using FourDotnet.BoogaBooster.DigitalTwin.Features.RequestRideStateTransition;
using FourDotnet.BoogaBooster.DigitalTwin.Features.SetGondolaBrake;
using FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEnginePower;
using FourDotnet.BoogaBooster.DigitalTwin.Features.SetMainEnginePower;
using FourDotnet.BoogaBooster.DigitalTwin.Features.StartRide;
using FourDotnet.BoogaBooster.DigitalTwin.Features.StopRide;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FourDotnet.BoogaBooster.DigitalTwin.Endpoints;

/// <summary>
/// HTTP endpoints owned by the DigitalTwin module (ADR-0007). The API host maps them
/// with <see cref="MapDigitalTwinEndpoints"/>; it contains no endpoint mappings
/// itself. Each endpoint maps its request to a command/query and dispatches to the
/// injected handler — no business logic lives here. Broken invariants surface as a
/// <see cref="DomainValidationException"/>, which is translated to a 400 response.
/// </summary>
public static class DigitalTwinEndpoints
{
    /// <summary>The request body for setting an engine's power.</summary>
    /// <param name="Percent">The throttle setting (0–100).</param>
    public sealed record SetPowerRequest(double Percent);

    /// <summary>The request body for boarding a passenger.</summary>
    public sealed record BoardPassengerRequest(int HubIndex, int GondolaIndex, string? Seat, double? WeightKg);

    /// <summary>The request body for working a gondola brake.</summary>
    public sealed record SetBrakeRequest(int HubIndex, int GondolaIndex, string? Brake);

    /// <summary>The request body for requesting a ride lifecycle transition.</summary>
    /// <param name="State">The target state name (e.g. "Loading", "Started", "EmergencyStop").</param>
    public sealed record SetStateRequest(string? State);

    public static IEndpointRouteBuilder MapDigitalTwinEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/ride").WithTags("DigitalTwin");

        group.MapGet("/telemetry", async (
            IQueryHandler<GetRideTelemetryQuery, RideTelemetry> handler,
            CancellationToken cancellationToken) =>
        {
            var telemetry = await handler.HandleAsync(new GetRideTelemetryQuery(), cancellationToken);
            return Results.Ok(telemetry);
        })
        .WithName("GetRideTelemetry");

        // Live telemetry broadcast over Server-Sent Events. The connection is held open
        // for the client's lifetime; full RideTelemetry snapshots are pushed at the
        // telemetry rate while the ride is running (see RideTelemetryStream).
        group.MapGet("/telemetry/stream", (
            RideTelemetryStream stream,
            CancellationToken cancellationToken) =>
            TypedResults.ServerSentEvents(stream.Stream(cancellationToken), eventType: "ride-telemetry"))
        .WithName("StreamRideTelemetry");

        group.MapPost("/main-power", (
            SetPowerRequest request,
            ICommandHandler<SetMainEnginePowerCommand> handler,
            CancellationToken cancellationToken) =>
            DispatchAsync(() => handler.HandleAsync(new SetMainEnginePowerCommand(request.Percent), cancellationToken)))
        .WithName("SetMainEnginePower");

        group.MapPost("/hub-power", (
            SetPowerRequest request,
            ICommandHandler<SetHubEnginePowerCommand> handler,
            CancellationToken cancellationToken) =>
            DispatchAsync(() => handler.HandleAsync(new SetHubEnginePowerCommand(request.Percent), cancellationToken)))
        .WithName("SetHubEnginePower");

        group.MapPost("/passengers", (
            BoardPassengerRequest request,
            ICommandHandler<BoardPassengerCommand> handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseSeat(request.Seat, out var seat))
            {
                return Task.FromResult(Results.BadRequest($"Unknown seat '{request.Seat}'. Expected Left or Right."));
            }

            return DispatchAsync(() => handler.HandleAsync(
                new BoardPassengerCommand(request.HubIndex, request.GondolaIndex, seat, request.WeightKg),
                cancellationToken));
        })
        .WithName("BoardPassenger");

        group.MapPost("/brake", (
            SetBrakeRequest request,
            ICommandHandler<SetGondolaBrakeCommand> handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseBrake(request.Brake, out var brake))
            {
                return Task.FromResult(Results.BadRequest($"Unknown brake state '{request.Brake}'. Expected Engaged or Released."));
            }

            return DispatchAsync(() => handler.HandleAsync(
                new SetGondolaBrakeCommand(request.HubIndex, request.GondolaIndex, brake),
                cancellationToken));
        })
        .WithName("SetGondolaBrake");

        // The lifecycle state machine is driven through /state. The /start and /stop
        // shortcuts are retained and delegate to the same machine (Started/Stopping).
        group.MapPost("/state", (
            SetStateRequest request,
            ICommandHandler<RequestRideStateTransitionCommand> handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseState(request.State, out var state))
            {
                return Task.FromResult(Results.BadRequest($"Unknown ride state '{request.State}'."));
            }

            return DispatchAsync(() => handler.HandleAsync(
                new RequestRideStateTransitionCommand(state),
                cancellationToken));
        })
        .WithName("RequestRideStateTransition");

        group.MapPost("/start", (
            ICommandHandler<StartRideCommand> handler,
            CancellationToken cancellationToken) =>
            DispatchAsync(() => handler.HandleAsync(new StartRideCommand(), cancellationToken)))
        .WithName("StartRide");

        group.MapPost("/stop", (
            ICommandHandler<StopRideCommand> handler,
            CancellationToken cancellationToken) =>
            DispatchAsync(() => handler.HandleAsync(new StopRideCommand(), cancellationToken)))
        .WithName("StopRide");

        return endpoints;
    }

    private static async Task<IResult> DispatchAsync(Func<Task> command)
    {
        try
        {
            await command();
            return Results.Accepted();
        }
        catch (DomainValidationException exception)
        {
            return Results.BadRequest(exception.Message);
        }
    }

    private static bool TryParseSeat(string? value, out SeatPosition seat)
        => Enum.TryParse(value, ignoreCase: true, out seat) && Enum.IsDefined(seat);

    private static bool TryParseBrake(string? value, out GondolaBrakeState brake)
        => Enum.TryParse(value, ignoreCase: true, out brake) && Enum.IsDefined(brake);

    private static bool TryParseState(string? value, out RideState state)
        => Enum.TryParse(value, ignoreCase: true, out state) && Enum.IsDefined(state);
}
