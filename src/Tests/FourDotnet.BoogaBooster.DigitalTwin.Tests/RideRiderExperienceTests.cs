using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the riders' experience at the level of the <see cref="Ride"/> aggregate: it
/// boards with the guests, changes only while the physics runs (docs/06 §6.7), and is
/// summarised in every telemetry frame as averages that are absent for an empty ride.
/// </summary>
public sealed class RideRiderExperienceTests
{
    private static readonly Func<TimeSpan> NoDelay = () => TimeSpan.Zero;

    private static BoardingPassenger Guest(double happiness, double preferredIntensity, double nausea) =>
        TestHelpers.Boarding(experience: new PassengerExperience(happiness, preferredIntensity, nausea));

    // --- Boarding carries the experience (4.2) ---

    [Fact]
    public void A_boarded_rider_carries_the_experience_they_boarded_with()
    {
        var ride = Ride.Create();

        ride.BoardGroup([Guest(happiness: 60d, preferredIntensity: 90d, nausea: 10d)], NoDelay);

        Assert.Equal(new RiderExperienceTelemetry(60d, 90d, 10d), ride.ToTelemetry().RiderExperience);
    }

    [Fact]
    public void A_boarding_group_with_a_missing_member_is_rejected_and_seats_nobody()
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(() => ride.BoardGroup([TestHelpers.Boarding(), null!], NoDelay));
        Assert.Equal(0, ride.Mill.BoardedPassengerCount);
    }

    // --- Telemetry summary (5.1) ---

    [Fact]
    public void The_frame_averages_the_experience_of_every_rider()
    {
        var ride = Ride.Create();

        ride.BoardGroup(
            [
                Guest(happiness: 60d, preferredIntensity: 50d, nausea: 0d),
                Guest(happiness: 90d, preferredIntensity: 100d, nausea: 50d),
            ],
            NoDelay);

        Assert.Equal(new RiderExperienceTelemetry(75d, 75d, 25d), ride.ToTelemetry().RiderExperience);
    }

    [Fact]
    public void The_frame_averages_over_riders_across_several_gondolas()
    {
        var ride = Ride.Create();

        ride.BoardGroup([Guest(40d, 60d, 0d), Guest(50d, 70d, 0d), Guest(90d, 80d, 30d)], NoDelay);

        var summary = ride.ToTelemetry().RiderExperience;
        Assert.Equal(60d, summary.AverageHappiness!.Value, precision: 12);
        Assert.Equal(70d, summary.AveragePreferredIntensity!.Value, precision: 12);
        Assert.Equal(10d, summary.AverageNausea!.Value, precision: 12);
    }

    [Fact]
    public void An_empty_ride_reports_no_averages()
    {
        var summary = Ride.Create().ToTelemetry().RiderExperience;

        Assert.Null(summary.AverageHappiness);
        Assert.Null(summary.AveragePreferredIntensity);
        Assert.Null(summary.AverageNausea);
    }

    // --- Ratings change only while the physics runs (4.4) ---

    [Fact]
    public void Riders_ratings_do_not_change_while_the_ride_is_loading()
    {
        var ride = Ride.Create();
        ride.BoardGroup([Guest(70d, 50d, 0d), Guest(70d, 50d, 0d)], NoDelay);
        var boarded = ride.ToTelemetry().RiderExperience;

        RideSafetyTests.Advance(ride, seconds: 10d);

        Assert.Equal(RideState.Loading, ride.CurrentState);
        Assert.Equal(boarded, ride.ToTelemetry().RiderExperience);
    }

    [Fact]
    public void Riders_ratings_change_while_the_ride_is_started()
    {
        var ride = StartedRideWithRiders();
        var atStart = ride.ToTelemetry().RiderExperience;

        RideSafetyTests.Advance(ride, seconds: 10d);

        var running = ride.ToTelemetry().RiderExperience;
        Assert.Equal(RideState.Started, ride.CurrentState);
        Assert.True(
            running.AverageHappiness > atStart.AverageHappiness,
            $"Happiness should rise while riding ({atStart.AverageHappiness} → {running.AverageHappiness}).");
    }

    [Fact]
    public void Riders_ratings_do_not_change_while_the_ride_is_offloading()
    {
        var ride = StartedRideWithRiders();
        RideSafetyTests.Advance(ride, seconds: 5d);
        ride.RequestTransition(RideState.Stopping);
        Assert.True(AdvanceUntil(ride, RideState.Offloading, maxSeconds: 30d), "The ride should come to rest and offload.");

        // The riders are still seated as offloading begins; hold on to them as they leave.
        var riders = SeatedRiders(ride);
        var beforeOffloading = riders.Select(r => r.Experience).ToArray();

        ride.Advance(TestHelpers.Dt);

        Assert.Equal(RideState.Idle, ride.CurrentState);
        Assert.Equal(beforeOffloading, riders.Select(r => r.Experience));
    }

    /// <summary>A ride with two secured riders, spun up under full hub power and started.</summary>
    private static Ride StartedRideWithRiders()
    {
        var ride = Ride.Create();
        ride.BoardGroup([Guest(60d, 60d, 0d), Guest(60d, 60d, 0d)], NoDelay);
        RideSafetyTests.Advance(ride, seconds: 0.1d); // riders secure their restraints
        ride.RequestTransition(RideState.Safe);
        ride.SetHubEnginePower(new EnginePower(100));
        ride.RequestTransition(RideState.Started);
        return ride;
    }

    private static IReadOnlyList<Passenger> SeatedRiders(Ride ride) =>
        ride.Mill.Hubs
            .SelectMany(hub => hub.Gondolas)
            .SelectMany(gondola => new[] { gondola.GetSeat(SeatPosition.Left), gondola.GetSeat(SeatPosition.Right) })
            .Select(seat => seat.Occupant)
            .OfType<Passenger>()
            .ToArray();

    private static bool AdvanceUntil(Ride ride, RideState target, double maxSeconds)
    {
        var steps = (int)(maxSeconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps && ride.CurrentState != target; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        return ride.CurrentState == target;
    }
}
