using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the ride-level rider experience: mood is frozen while the ride is not in
/// motion and evolves only while it is (<c>docs/06 §6.2</c>), passengers leave with
/// their final mood, and the telemetry roll-up (design D9) averages the seated
/// passengers.
/// </summary>
/// <remarks>
/// The proving rider prefers the tamest ride (<see cref="RiderProfile.MinPreferredIntensity"/>,
/// 0.1): a stationary gondola's intensity of 0 is within tolerance of that, so their
/// happiness rises on every tick <em>on which the experience runs</em>. Unchanged
/// happiness therefore proves the experience did not run, not merely that nothing was felt.
/// </remarks>
public sealed class RideRiderMoodTests
{
    private const double Tolerance = 1e-9;
    private const double StartHappiness = 0.5;

    private static readonly Func<TimeSpan> NoDelay = () => TimeSpan.Zero;

    private static Passenger ProvingRider() =>
        new(new PassengerWeight(75d), new RiderProfile(RiderProfile.MinPreferredIntensity, StartHappiness, 0d));

    private static Passenger Rider(double happiness, double nausea, double preferred = 0.5) =>
        new(new PassengerWeight(75d), new RiderProfile(preferred, happiness, nausea));

    private static Passenger Seated(Ride ride) =>
        Assert.IsType<Passenger>(ride.Mill.GetHub(0).GetGondola(0).GetSeat(SeatPosition.Left).Occupant);

    /// <summary>
    /// Advances by whole fixed steps and returns the simulated seconds actually elapsed —
    /// the step is a <see cref="TimeSpan"/> quantised to 100 ns, so that is a hair under
    /// <paramref name="seconds"/>.
    /// </summary>
    private static double Advance(Ride ride, double seconds)
    {
        var steps = (int)Math.Round(seconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        return steps * TestHelpers.Dt.TotalSeconds;
    }

    /// <summary>A ride with the proving rider seated, in <see cref="RideState.Loading"/>.</summary>
    private static Ride LoadingWithProvingRider()
    {
        var ride = Ride.Create();
        ride.BoardPassenger(0, 0, SeatPosition.Left, ProvingRider(), TimeSpan.Zero);
        return ride;
    }

    [Fact]
    public void Advance_WhileLoading_LeavesMoodUnchanged()
    {
        var ride = LoadingWithProvingRider();

        Advance(ride, seconds: 10d);

        Assert.Equal(RideState.Loading, ride.CurrentState);
        Assert.Equal(StartHappiness, Seated(ride).Happiness);
        Assert.Equal(0d, Seated(ride).Nausea);
    }

    [Fact]
    public void Advance_WhileSafe_LeavesMoodUnchanged()
    {
        var ride = LoadingWithProvingRider();
        ride.Advance(TestHelpers.Dt); // the zero-delay restraint secures on the first tick
        ride.RequestTransition(RideState.Safe);

        Advance(ride, seconds: 10d);

        Assert.Equal(RideState.Safe, ride.CurrentState);
        Assert.Equal(StartHappiness, Seated(ride).Happiness);
    }

    [Fact]
    public void Advance_WhileStarted_EvolvesMood()
    {
        var ride = LoadingWithProvingRider();
        ride.Advance(TestHelpers.Dt);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);

        var elapsed = Advance(ride, seconds: 2d);

        Assert.Equal(RideState.Started, ride.CurrentState);
        Assert.Equal(StartHappiness + (RideParameters.HappinessGainPerSecond * elapsed), Seated(ride).Happiness, Tolerance);
        Assert.Equal(0.7, Seated(ride).Happiness, 1e-5);
    }

    [Fact]
    public void Advance_WhileOffloading_ReturnsTheLeavingPassengersWithTheMoodTheyStoppedWith()
    {
        var ride = LoadingWithProvingRider();
        ride.Advance(TestHelpers.Dt);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        Advance(ride, seconds: 2d);
        var rider = Seated(ride);

        // The unpowered ride is already at rest, so the first Stopping tick settles it
        // into Offloading (running the physics one last time on the way).
        ride.RequestTransition(RideState.Stopping);
        ride.Advance(TestHelpers.Dt);
        Assert.Equal(RideState.Offloading, ride.CurrentState);
        var happinessAtOffloading = rider.Happiness;

        var departed = ride.Advance(TestHelpers.Dt);

        var left = Assert.Single(departed);
        Assert.Same(rider, left);
        Assert.Equal(happinessAtOffloading, left.Happiness);
        Assert.Equal(RideState.Idle, ride.CurrentState);
    }

    [Fact]
    public void Advance_OnAnOrdinaryTick_ReturnsNobody()
    {
        var ride = LoadingWithProvingRider();

        var departed = ride.Advance(TestHelpers.Dt);

        Assert.Empty(departed);
    }

    [Fact]
    public void BoardGroup_RejectsAGroupWithAMissingMember()
    {
        var ride = Ride.Create();

        Assert.Throws<Core.DomainValidationException>(
            () => ride.BoardGroup([Rider(0.7, 0), null!], NoDelay));
        Assert.True(ride.Mill.IsEmpty);
    }

    // --- The telemetry roll-up (design D9) ---

    [Fact]
    public void RiderMood_AveragesTheSeatedPassengers()
    {
        var ride = Ride.Create();
        ride.BoardGroup([Rider(happiness: 0.6, nausea: 0.2), Rider(happiness: 0.8, nausea: 0.4)], NoDelay);

        var riders = ride.ToTelemetry().Riders;

        Assert.Equal(2, riders.RiderCount);
        Assert.NotNull(riders.AverageHappiness);
        Assert.NotNull(riders.AverageNausea);
        Assert.Equal(0.7, riders.AverageHappiness.Value, Tolerance);
        Assert.Equal(0.3, riders.AverageNausea.Value, Tolerance);
    }

    [Fact]
    public void RiderMood_OnAnEmptyRide_HasNoAverages()
    {
        var telemetry = Ride.Create().ToTelemetry();

        Assert.Equal(new RiderMoodTelemetry(0, null, null), telemetry.Riders);
    }

    [Fact]
    public void RiderMood_CountMatchesTheBoardedPassengerCount()
    {
        var ride = Ride.Create();
        ride.BoardGroup([Rider(0.7, 0), Rider(0.7, 0), Rider(0.7, 0)], NoDelay);

        var telemetry = ride.ToTelemetry();

        Assert.Equal(3, telemetry.BoardedPassengerCount);
        Assert.Equal(telemetry.BoardedPassengerCount, telemetry.Riders.RiderCount);
    }

    [Fact]
    public void RiderMood_FollowsTheRidersOverSuccessiveFrames()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 11));
        store.BoardGroup([ProvingRider()]);
        AdvanceStore(store, seconds: RideParameters.MaxRestraintCloseDelay.TotalSeconds + 1d);
        store.RequestStateTransition(RideState.Safe);
        store.StartRide();

        var first = store.Advance(TestHelpers.Dt).Riders;
        AdvanceStore(store, seconds: 1d);
        var second = store.Advance(TestHelpers.Dt).Riders;

        Assert.NotNull(first.AverageHappiness);
        Assert.NotNull(second.AverageHappiness);
        Assert.True(second.AverageHappiness > first.AverageHappiness, "The matched rider's average should keep rising.");
        Assert.Equal(1, second.RiderCount);
    }

    [Fact]
    public void RiderMood_AfterOffloading_IsEmptyAgain()
    {
        var ride = LoadingWithProvingRider();
        ride.Advance(TestHelpers.Dt);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        ride.RequestTransition(RideState.Stopping);
        Advance(ride, seconds: 1d);

        Assert.Equal(RideState.Idle, ride.CurrentState);
        Assert.Equal(new RiderMoodTelemetry(0, null, null), ride.ToTelemetry().Riders);
    }

    private static void AdvanceStore(RideStore store, double seconds)
    {
        var steps = (int)Math.Round(seconds / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            store.Advance(TestHelpers.Dt);
        }
    }
}
