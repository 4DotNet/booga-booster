using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the ride half of the passenger-mood capability (docs/06-rider-mood.md):
/// the passenger's identity and clamped mood, felt G, the on-ride happiness and
/// nausea rules at the fixed 1/120 s step, the sustained max-G penalty, and the
/// last-offload snapshot.
/// </summary>
public sealed class RiderMoodTests
{
    private static readonly double Dt = TestHelpers.Dt.TotalSeconds;

    private static readonly Func<TimeSpan> NoDelay = () => TimeSpan.Zero;

    private static int Steps(double seconds) => (int)Math.Round(seconds / Dt);

    // --- Passenger and seed ---

    [Fact]
    public void Seed_CarriesIdentityAndMoodIntoThePassenger()
    {
        var passenger = new Passenger(TestHelpers.Guest(42, happiness: 70d, preferredG: 3.1d, nausea: 12d, kilograms: 80d));

        Assert.Equal(42L, passenger.GuestNumber);
        Assert.Equal(70d, passenger.Happiness);
        Assert.Equal(3.1d, passenger.PreferredG);
        Assert.Equal(12d, passenger.Nausea);
        Assert.Equal(80d, passenger.Weight.Kilograms);
    }

    [Fact]
    public void AnAnonymousPassenger_HasNoGuestNumberAndTheDefaultMood()
    {
        var passenger = Passenger.OfWeight(80d);

        Assert.Null(passenger.GuestNumber);
        Assert.Equal(RideParameters.DefaultPassengerHappiness, passenger.Happiness);
        Assert.Equal(RideParameters.DefaultPassengerPreferredG, passenger.PreferredG);
        Assert.Equal(0d, passenger.Nausea);
    }

    [Theory]
    [InlineData(0L, 75d, 3d, 0d)]
    [InlineData(1L, -1d, 3d, 0d)]
    [InlineData(1L, 101d, 3d, 0d)]
    [InlineData(1L, 75d, 2.2d, 0d)]
    [InlineData(1L, 75d, 4.6d, 0d)]
    [InlineData(1L, 75d, 3d, -1d)]
    [InlineData(1L, 75d, 3d, 101d)]
    [InlineData(1L, double.NaN, 3d, 0d)]
    public void Seed_WithAnInvalidValue_Throws(long guestNumber, double happiness, double preferredG, double nausea)
    {
        Assert.Throws<DomainValidationException>(
            () => new PassengerSeed(guestNumber, new PassengerWeight(75d), happiness, preferredG, nausea));
    }

    [Fact]
    public void Nausea_SaturatesAt100()
    {
        var passenger = new Passenger(TestHelpers.Guest(1, nausea: 90d));

        passenger.GainNausea(RideParameters.MaxGNauseaPenalty);

        Assert.Equal(100d, passenger.Nausea);
    }

    [Fact]
    public void Happiness_SaturatesAt100()
    {
        var passenger = new Passenger(TestHelpers.Guest(1, happiness: 99d));

        passenger.GainHappiness(5d);

        Assert.Equal(100d, passenger.Happiness);
        Assert.Equal(DomainModelState.New, passenger.State);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void AMoodGain_MustBeFiniteAndNonNegative(double points)
    {
        var passenger = new Passenger(TestHelpers.Guest(1));

        Assert.Throws<DomainValidationException>(() => passenger.GainHappiness(points));
        Assert.Throws<DomainValidationException>(() => passenger.GainNausea(points));
    }

    // --- Felt G ---

    [Fact]
    public void AGondolaAtRest_FeelsOneG()
    {
        var gondola = new Gondola(0, 0);

        gondola.AdvancePhysics(millAngle: 0d, millOmega: 0d, hubAngle: 0d, hubOmega: 0d, dt: Dt);

        Assert.Equal(1d, gondola.FeltG, 9);
        Assert.Equal(1d, gondola.ToTelemetry().FeltG, 9);
    }

    [Fact]
    public void HorizontalLoad_AddsToGravity()
    {
        Assert.Equal(Math.Sqrt(5d), Gondola.FeltGFrom(forwardG: 2d, lateralG: 0d), 9);
        Assert.Equal(2.24d, Gondola.FeltGFrom(forwardG: 2d, lateralG: 0d), 2);
        Assert.Equal(Gondola.FeltGFrom(0d, 2d), Gondola.FeltGFrom(2d, 0d), 9);
    }

    [Fact]
    public void AGondolaInTheField_ReportsFeltGFromItsComponents()
    {
        var gondola = new Gondola(0, 0);

        gondola.AdvancePhysics(millAngle: 0d, millOmega: 1.5d, hubAngle: 0d, hubOmega: 0d, dt: Dt);

        Assert.True(gondola.FeltG > 1d);
        Assert.Equal(Gondola.FeltGFrom(gondola.ForwardG, gondola.LateralG), gondola.FeltG, 12);
    }

    // --- Happiness gain ---

    [Fact]
    public void APerfectMatch_GivesTheFullGain()
    {
        var (gondola, rider) = Seated(preferredG: 3d, happiness: 50d);

        Hold(gondola, feltG: 3d, seconds: 5d);

        Assert.Equal(60d, rider.Happiness, 1e-3);
    }

    [Fact]
    public void AHalfBandMiss_GivesHalfTheGain()
    {
        var (gondola, rider) = Seated(preferredG: 3d, happiness: 50d);

        Hold(gondola, feltG: 3.5d, seconds: 5d);

        Assert.Equal(55d, rider.Happiness, 1e-3);
    }

    [Fact]
    public void FarFromThePreference_GivesNoGain()
    {
        var (gondola, rider) = Seated(preferredG: 4d, happiness: 50d);

        Hold(gondola, feltG: 1.5d, seconds: 5d);

        Assert.Equal(50d, rider.Happiness);
        Assert.Equal(0d, rider.Nausea);
    }

    // --- Nausea growth ---

    [Fact]
    public void Overshoot_GrowsNauseaExponentially()
    {
        var (gondola, rider) = Seated(preferredG: 2.25d);

        Hold(gondola, feltG: 4d, seconds: 10d);

        // Continuous solution 5 × (e − 1) ≈ 8.59; the 1/120 s Euler step lands within 0.01.
        Assert.Equal(5d * (Math.E - 1d), rider.Nausea, 0.01d);
        Assert.Equal(8.6d, rider.Nausea, 0.05d);
    }

    [Fact]
    public void WithinTolerance_CausesNoNausea()
    {
        var (gondola, rider) = Seated(preferredG: 2.5d);

        Hold(gondola, feltG: 3.5d, seconds: 10d);

        Assert.Equal(0d, rider.Nausea);
    }

    [Fact]
    public void BothSeats_AreAdvanced()
    {
        var gondola = new Gondola(0, 0);
        var left = new Passenger(TestHelpers.Guest(1, preferredG: 3d, happiness: 50d));
        var right = new Passenger(TestHelpers.Guest(2, preferredG: 3d, happiness: 60d));
        gondola.Board(SeatPosition.Left, left, TimeSpan.Zero);
        gondola.Board(SeatPosition.Right, right, TimeSpan.Zero);

        Hold(gondola, feltG: 3d, seconds: 1d);

        Assert.Equal(52d, left.Happiness, 1e-3);
        Assert.Equal(62d, right.Happiness, 1e-3);
    }

    // --- Sustained max-G penalty ---

    [Fact]
    public void OneLongStretch_GivesOnePenalty()
    {
        var (gondola, rider) = Seated(preferredG: RideParameters.MaxGForce);

        Hold(gondola, feltG: RideParameters.MaxGForce, seconds: 3d);

        Assert.Equal(RideParameters.MaxGNauseaPenalty, rider.Nausea);
    }

    [Fact]
    public void TwoStretches_GiveTwoPenalties()
    {
        var (gondola, rider) = Seated(preferredG: RideParameters.MaxGForce);

        Hold(gondola, feltG: RideParameters.MaxGForce, seconds: 1.5d);
        Hold(gondola, feltG: 4d, seconds: 0.5d);
        Hold(gondola, feltG: RideParameters.MaxGForce, seconds: 1.5d);

        Assert.Equal(2d * RideParameters.MaxGNauseaPenalty, rider.Nausea);
    }

    [Fact]
    public void AShortSpike_GivesNoPenalty()
    {
        var (gondola, rider) = Seated(preferredG: RideParameters.MaxGForce);

        Hold(gondola, feltG: RideParameters.MaxGForce, seconds: 0.5d);
        Hold(gondola, feltG: 4d, seconds: 0.5d);
        Hold(gondola, feltG: RideParameters.MaxGForce, seconds: 0.5d);

        Assert.Equal(0d, rider.Nausea);
    }

    // --- Mood only changes while the physics runs ---

    [Fact]
    public void Loading_DoesNotChangeRiderMood()
    {
        var ride = Ride.Create();
        ride.BoardGroup([TestHelpers.Guest(7, happiness: 70d, preferredG: 2.25d)], NoDelay);

        for (var i = 0; i < Steps(5d); i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        var seat = SeatOf(ride.ToTelemetry(), 7);
        Assert.Equal(70d, seat.Happiness);
        Assert.Equal(0d, seat.Nausea);
    }

    // --- Identity and telemetry ---

    [Fact]
    public void BoardedRiders_KeepTheirGuestNumberAndPreferredG()
    {
        var ride = Ride.Create();

        ride.BoardGroup([TestHelpers.Guest(42, happiness: 70d, preferredG: 3.1d, nausea: 12d)], NoDelay);

        var seat = SeatOf(ride.ToTelemetry(), 42);
        Assert.Equal(70d, seat.Happiness);
        Assert.Equal(3.1d, seat.PreferredG);
        Assert.Equal(12d, seat.Nausea);
    }

    [Fact]
    public void AnEmptySeat_ReportsNullMood()
    {
        var seat = Ride.Create().ToTelemetry().Gondolas[0].Seats[0];

        Assert.False(seat.IsOccupied);
        Assert.Null(seat.GuestNumber);
        Assert.Null(seat.Happiness);
        Assert.Null(seat.PreferredG);
        Assert.Null(seat.Nausea);
    }

    // --- Last offload ---

    [Fact]
    public void BeforeAnyOffload_TheLastOffloadIsEmpty()
    {
        var lastOffload = Ride.Create().ToTelemetry().LastOffload;

        Assert.Equal(0, lastOffload.Counter);
        Assert.Empty(lastOffload.Riders);
    }

    [Fact]
    public void AnOffload_RecordsTheRidersAndTheirFinalMood()
    {
        var ride = Ride.Create();
        var guests = Enumerable.Range(1, 8)
            .Select(n => TestHelpers.Guest(n, happiness: 60d + n, nausea: n))
            .ToArray();
        BoardOneGondolaPerHub(ride, guests);

        RunOneRide(ride);

        var lastOffload = ride.ToTelemetry().LastOffload;
        Assert.Equal(RideState.Idle, ride.CurrentState);
        Assert.Equal(1, lastOffload.Counter);
        Assert.Equal(
            guests.Select(g => g.GuestNumber).Order(),
            lastOffload.Riders.Select(r => r.GuestNumber).Order());
        Assert.All(lastOffload.Riders, r =>
        {
            var guest = guests.Single(g => g.GuestNumber == r.GuestNumber);
            Assert.True(r.Happiness >= guest.Happiness);
            Assert.True(r.Nausea >= guest.Nausea);
        });
    }

    [Fact]
    public void EachOffload_BumpsTheCounterByOne()
    {
        var ride = Ride.Create();

        BoardOneGondolaPerHub(ride, [.. Enumerable.Range(1, 8).Select(n => TestHelpers.Guest(n))]);
        RunOneRide(ride);
        BoardOneGondolaPerHub(ride, [.. Enumerable.Range(9, 8).Select(n => TestHelpers.Guest(n))]);
        RunOneRide(ride);

        var lastOffload = ride.LastOffload;
        Assert.Equal(2, lastOffload.Counter);
        Assert.Equal(Enumerable.Range(9, 8).Select(n => (long?)n), lastOffload.Riders.Select(r => r.GuestNumber).Order());
    }

    [Fact]
    public void AnEmptyOffload_DoesNotBumpTheCounter()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);

        RunOneRide(ride);

        Assert.Equal(0, ride.LastOffload.Counter);
    }

    // --- Helpers ---

    private static (Gondola Gondola, Passenger Rider) Seated(double preferredG, double happiness = 50d)
    {
        var gondola = new Gondola(0, 0);
        var rider = new Passenger(TestHelpers.Guest(1, happiness: happiness, preferredG: preferredG));
        gondola.Board(SeatPosition.Left, rider, TimeSpan.Zero);
        return (gondola, rider);
    }

    private static void Hold(Gondola gondola, double feltG, double seconds)
    {
        for (var i = 0; i < Steps(seconds); i++)
        {
            gondola.AdvanceRiderMood(feltG, Dt);
        }
    }

    private static SeatTelemetry SeatOf(RideTelemetry telemetry, long guestNumber) =>
        telemetry.Gondolas.SelectMany(g => g.Seats).Single(s => s.GuestNumber == guestNumber);

    /// <summary>Seats eight guests two to a gondola, one gondola per hub, so the load is balanced.</summary>
    private static void BoardOneGondolaPerHub(Ride ride, IReadOnlyList<PassengerSeed> guests)
    {
        ride.BoardGroup(
            guests,
            NoDelay,
            selectGondolas: (_, required) => [.. Enumerable.Range(0, required).Select(i => i * RideParameters.GondolasPerHub)]);
    }

    /// <summary>Secures restraints, runs the ride briefly under power, stops it and lets it offload back to idle.</summary>
    private static void RunOneRide(Ride ride)
    {
        ride.Advance(TestHelpers.Dt); // riders secure their restraints (no delay)
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);
        ride.SetMainEnginePower(new EnginePower(50d));

        for (var i = 0; i < Steps(5d); i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        ride.RequestTransition(RideState.Stopping);
        for (var i = 0; i < Steps(60d) && ride.CurrentState != RideState.Idle; i++)
        {
            ride.Advance(TestHelpers.Dt);
        }

        Assert.Equal(RideState.Idle, ride.CurrentState);
    }
}
