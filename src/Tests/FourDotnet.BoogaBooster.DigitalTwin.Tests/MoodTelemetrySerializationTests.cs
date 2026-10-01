using System.Text.Json;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Pins the wire shape of the passenger-mood additions to the telemetry payload as
/// the HTTP/SSE endpoints serialise it (ASP.NET Core's web JSON defaults): rider
/// fields on every seat, felt G on every gondola, and the last offload.
/// </summary>
public sealed class MoodTelemetrySerializationTests
{
    private static JsonElement Serialize(RideTelemetry telemetry) =>
        JsonSerializer.SerializeToElement(telemetry, JsonSerializerOptions.Web);

    [Fact]
    public void AFreshRide_SerialisesNullRidersAndAnEmptyLastOffload()
    {
        var json = Serialize(Ride.Create().ToTelemetry());

        var lastOffload = json.GetProperty("lastOffload");
        Assert.Equal(0, lastOffload.GetProperty("counter").GetInt32());
        Assert.Equal(0, lastOffload.GetProperty("riders").GetArrayLength());

        var gondola = json.GetProperty("gondolas")[0];
        Assert.Equal(1d, gondola.GetProperty("feltG").GetDouble());

        var seat = gondola.GetProperty("seats")[0];
        Assert.Equal(JsonValueKind.Null, seat.GetProperty("guestNumber").ValueKind);
        Assert.Equal(JsonValueKind.Null, seat.GetProperty("happiness").ValueKind);
        Assert.Equal(JsonValueKind.Null, seat.GetProperty("preferredG").ValueKind);
        Assert.Equal(JsonValueKind.Null, seat.GetProperty("nausea").ValueKind);
    }

    [Fact]
    public void AnOccupiedSeat_SerialisesItsRider()
    {
        var ride = Ride.Create();
        ride.BoardGroup([TestHelpers.Guest(42, happiness: 70d, preferredG: 3.1d, nausea: 12d)], () => TimeSpan.Zero);

        var seat = Serialize(ride.ToTelemetry()).GetProperty("gondolas")[0].GetProperty("seats")[0];

        Assert.Equal(42L, seat.GetProperty("guestNumber").GetInt64());
        Assert.Equal(70d, seat.GetProperty("happiness").GetDouble());
        Assert.Equal(3.1d, seat.GetProperty("preferredG").GetDouble());
        Assert.Equal(12d, seat.GetProperty("nausea").GetDouble());
    }

    [Fact]
    public void AnOffload_SerialisesItsRiders()
    {
        var telemetry = Ride.Create().ToTelemetry() with
        {
            LastOffload = new LastOffloadTelemetry(3, [new OffloadedRiderTelemetry(7, 88d, 72d)]),
        };

        var lastOffload = Serialize(telemetry).GetProperty("lastOffload");

        Assert.Equal(3, lastOffload.GetProperty("counter").GetInt32());
        var rider = lastOffload.GetProperty("riders")[0];
        Assert.Equal(7L, rider.GetProperty("guestNumber").GetInt64());
        Assert.Equal(88d, rider.GetProperty("happiness").GetDouble());
        Assert.Equal(72d, rider.GetProperty("nausea").GetDouble());
    }
}
