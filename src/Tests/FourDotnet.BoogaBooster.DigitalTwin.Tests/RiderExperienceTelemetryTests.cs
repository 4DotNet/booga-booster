using System.Text.Json;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the rider-experience summary on every telemetry frame (design D7,
/// ride-telemetry-streaming "Full telemetry payload"): the averages over everyone on
/// board, absent rather than zero when the ride is empty, under the JSON names the
/// dashboard reads.
/// </summary>
public sealed class RiderExperienceTelemetryTests
{
    private static readonly Func<TimeSpan> NoDelay = () => TimeSpan.Zero;

    [Fact]
    public void A_frame_summarises_the_experience_of_everyone_on_board()
    {
        var ride = Ride.Create();
        ride.BoardGroup(
            [
                new PassengerBoarding(new PassengerWeight(75d), new RiderExperience(70, 50, 0)),
                new PassengerBoarding(new PassengerWeight(75d), new RiderExperience(90, 100, 20)),
            ],
            NoDelay);

        var summary = ride.ToTelemetry().RiderExperience;

        Assert.Equal(2, summary.RiderCount);
        Assert.Equal(80d, summary.AverageHappiness);
        Assert.Equal(75d, summary.AveragePreferredIntensity);
        Assert.Equal(10d, summary.AverageNausea);
    }

    [Fact]
    public void A_frame_of_an_empty_ride_reports_no_averages()
    {
        var summary = Ride.Create().ToTelemetry().RiderExperience;

        Assert.Equal(0, summary.RiderCount);
        Assert.Null(summary.AverageHappiness);
        Assert.Null(summary.AveragePreferredIntensity);
        Assert.Null(summary.AverageNausea);
    }

    [Fact]
    public void The_rider_count_matches_the_boarded_passenger_count()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 3));
        store.BoardGroup([TestHelpers.Boarding(), TestHelpers.Boarding(), TestHelpers.Boarding()]);

        var telemetry = store.GetTelemetry();

        Assert.Equal(telemetry.BoardedPassengerCount, telemetry.RiderExperience.RiderCount);
    }

    [Fact]
    public void A_running_ride_streams_the_riders_changing_experience()
    {
        var ride = Ride.Create();
        ride.BoardGroup(
            [
                new PassengerBoarding(new PassengerWeight(75d), new RiderExperience(70, 0, 0)),
                new PassengerBoarding(new PassengerWeight(75d), new RiderExperience(70, 0, 0)),
            ],
            NoDelay);
        ride.Advance(TestHelpers.Dt); // the riders secure their restraints
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        var before = ride.ToTelemetry().RiderExperience;

        // Tame-preferring riders sit at an exact match while the ride is still at rest.
        for (var i = 0; i < 240; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        Assert.True(ride.ToTelemetry().RiderExperience.AverageHappiness > before.AverageHappiness);
    }

    [Fact]
    public void The_summary_serialises_under_the_names_the_dashboard_reads()
    {
        var ride = Ride.Create();
        ride.BoardGroup([new PassengerBoarding(new PassengerWeight(75d), new RiderExperience(70, 50, 5))], NoDelay);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(ride.ToTelemetry(), JsonSerializerOptions.Web));

        var summary = json.RootElement.GetProperty("riderExperience");
        Assert.Equal(1, summary.GetProperty("riderCount").GetInt32());
        Assert.Equal(70d, summary.GetProperty("averageHappiness").GetDouble());
        Assert.Equal(50d, summary.GetProperty("averagePreferredIntensity").GetDouble());
        Assert.Equal(5d, summary.GetProperty("averageNausea").GetDouble());
    }

    [Fact]
    public void An_empty_summary_serialises_its_averages_as_null()
    {
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(Ride.Create().ToTelemetry(), JsonSerializerOptions.Web));

        var summary = json.RootElement.GetProperty("riderExperience");
        Assert.Equal(0, summary.GetProperty("riderCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("averageHappiness").ValueKind);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("averagePreferredIntensity").ValueKind);
        Assert.Equal(JsonValueKind.Null, summary.GetProperty("averageNausea").ValueKind);
    }
}
