using System.Threading;
using System.Threading.Tasks;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using FourDotnet.BoogaBooster.DigitalTwin.Features.BoardPassenger;
using FourDotnet.BoogaBooster.DigitalTwin.Features.GetRideTelemetry;
using FourDotnet.BoogaBooster.DigitalTwin.Features.SetGondolaBrake;
using FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEnginePower;
using FourDotnet.BoogaBooster.DigitalTwin.Features.SetMainEnginePower;
using FourDotnet.BoogaBooster.DigitalTwin.Features.StartRide;
using FourDotnet.BoogaBooster.DigitalTwin.Features.StopRide;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class TelemetryAndCommandTests
{
    private static RideStore NewStore() => new(new RandomRideEventSampler(seed: 1));

    [Fact]
    public void Telemetry_snapshot_has_the_full_ride_shape()
    {
        var telemetry = NewStore().GetTelemetry();

        Assert.Equal(RideParameters.HubCount, telemetry.Hubs.Count);
        Assert.Equal(RideParameters.HubCount * RideParameters.GondolasPerHub, telemetry.Gondolas.Count);
        Assert.All(telemetry.Gondolas, g => Assert.Equal(RideParameters.SeatsPerGondola, g.Seats.Count));
        Assert.Equal(RideState.Idle, telemetry.State);
    }

    [Fact]
    public void Setting_main_power_emits_the_consumed_watts()
    {
        var telemetry = NewStore().SetMainEnginePower(new EnginePower(100));

        Assert.Equal(RideParameters.MillMaxPowerWatts, telemetry.Mill.PowerWatts, 6);
    }

    [Fact]
    public void Setting_hub_power_applies_to_every_hub()
    {
        var telemetry = NewStore().SetHubEnginePower(new EnginePower(50));

        Assert.All(telemetry.Hubs, h => Assert.Equal(RideParameters.HubMaxPowerWatts * 0.5d, h.PowerWatts, 6));
    }

    [Fact]
    public void Releasing_a_brake_through_the_store_is_reflected_in_telemetry()
    {
        var store = NewStore();

        var telemetry = store.SetGondolaBrake(2, 1, GondolaBrakeState.Released);

        var gondola = System.Linq.Enumerable.Single(telemetry.Gondolas, g => g is { HubIndex: 2, Index: 1 });
        Assert.Equal(GondolaBrakeState.Released, gondola.Brake);
    }

    [Fact]
    public async Task All_command_handlers_drive_the_store()
    {
        var store = NewStore();
        var token = CancellationToken.None;

        await new SetMainEnginePowerCommandHandler(store).HandleAsync(new SetMainEnginePowerCommand(80), token);
        await new SetHubEnginePowerCommandHandler(store).HandleAsync(new SetHubEnginePowerCommand(60), token);
        await new BoardPassengerCommandHandler(store).HandleAsync(
            new BoardPassengerCommand(0, 0, SeatPosition.Left, 90d), token);
        await new BoardPassengerCommandHandler(store).HandleAsync(
            new BoardPassengerCommand(0, 1, SeatPosition.Right, null), token);
        await new SetGondolaBrakeCommandHandler(store).HandleAsync(
            new SetGondolaBrakeCommand(0, 0, GondolaBrakeState.Released), token);

        var query = await new GetRideTelemetryQueryHandler(store).HandleAsync(new GetRideTelemetryQuery(), token);
        Assert.Equal(RideParameters.MillMaxPowerWatts * 0.8d, query.Mill.PowerWatts, 6);
        Assert.Equal(RideState.Boarding, query.State);
    }

    [Fact]
    public async Task Start_and_stop_handlers_move_an_empty_ride_through_its_states()
    {
        var store = NewStore();
        var token = CancellationToken.None;

        // An empty ride is safe to start.
        await new StartRideCommandHandler(store).HandleAsync(new StartRideCommand(), token);
        Assert.Equal(RideState.Running, store.GetTelemetry().State);

        await new StopRideCommandHandler(store).HandleAsync(new StopRideCommand(), token);
        Assert.Equal(RideState.Stopping, store.GetTelemetry().State);
    }
}
