using System.Threading;
using System.Threading.Tasks;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using FourDotnet.BoogaBooster.DigitalTwin.Features.SetHubEngineDirection;
using FourDotnet.BoogaBooster.DigitalTwin.Features.SetMainEngineDirection;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// The command surface and telemetry contract for the commanded motor direction:
/// the store setters, the CQRS handlers, the reported direction, and the signed rpm.
/// </summary>
public sealed class DirectionTelemetryAndCommandTests
{
    private static RideStore NewStore() => new(new RandomRideEventSampler(seed: 1));

    [Fact]
    public void A_fresh_ride_reports_forward_for_the_mill_and_every_hub()
    {
        var telemetry = NewStore().GetTelemetry();

        Assert.Equal(MotorDirection.Forward, telemetry.Mill.Direction);
        Assert.All(telemetry.Hubs, h => Assert.Equal(MotorDirection.Forward, h.Direction));
    }

    [Fact]
    public void Setting_the_mill_direction_is_reflected_in_telemetry_and_leaves_speed_at_zero()
    {
        var telemetry = NewStore().SetMainEngineDirection(MotorDirection.Reverse);

        Assert.Equal(MotorDirection.Reverse, telemetry.Mill.Direction);
        Assert.Equal(0d, telemetry.Mill.Rpm);
    }

    [Fact]
    public void Setting_the_hub_direction_applies_to_every_hub()
    {
        var telemetry = NewStore().SetHubEngineDirection(MotorDirection.Reverse);

        Assert.All(telemetry.Hubs, h => Assert.Equal(MotorDirection.Reverse, h.Direction));
    }

    [Fact]
    public void A_reversed_running_mill_reports_reverse_and_a_negative_rpm()
    {
        var mill = new GreatMill();
        mill.SetPower(new EnginePower(100));
        mill.SetDirection(MotorDirection.Reverse);
        TestHelpers.StepMill(mill, steps: 120 * 5);

        var telemetry = mill.ToTelemetry();

        Assert.Equal(MotorDirection.Reverse, telemetry.Direction);
        Assert.True(telemetry.Rpm < 0d, $"A reversed running mill should report a negative rpm (was {telemetry.Rpm}).");
    }

    [Fact]
    public async Task The_direction_command_handlers_drive_the_store()
    {
        var store = NewStore();
        var token = CancellationToken.None;

        await new SetMainEngineDirectionCommandHandler(store)
            .HandleAsync(new SetMainEngineDirectionCommand(MotorDirection.Reverse), token);
        await new SetHubEngineDirectionCommandHandler(store)
            .HandleAsync(new SetHubEngineDirectionCommand(MotorDirection.Reverse), token);

        var telemetry = store.GetTelemetry();
        Assert.Equal(MotorDirection.Reverse, telemetry.Mill.Direction);
        Assert.All(telemetry.Hubs, h => Assert.Equal(MotorDirection.Reverse, h.Direction));
    }

    [Theory]
    [InlineData("Forward", true)]
    [InlineData("forward", true)]
    [InlineData("Reverse", true)]
    [InlineData("reverse", true)]
    [InlineData("sideways", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void The_endpoint_direction_parsing_contract_accepts_only_known_names(string? value, bool expectedValid)
    {
        // Mirrors DigitalTwinEndpoints.TryParseDirection: unknown values are rejected
        // so the endpoint answers 400 rather than dispatching a command.
        var parsed = Enum.TryParse<MotorDirection>(value, ignoreCase: true, out var direction)
            && Enum.IsDefined(direction);

        Assert.Equal(expectedValid, parsed);
    }
}
