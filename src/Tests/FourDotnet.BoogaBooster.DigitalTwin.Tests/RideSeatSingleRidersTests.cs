using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the ride's two capacity measures (<see cref="Ride.FreeSeatCount"/> and
/// <see cref="Ride.GroupBoardableSeats"/>) and <see cref="Ride.SeatSingleRiders"/>:
/// stranger pairing, the capacity guard measured in seats, injected seat selection,
/// state gating, all-or-nothing seating — and that groups still take only whole
/// empty gondolas.
/// </summary>
public sealed class RideSeatSingleRidersTests
{
    private const int TotalGondolas = RideParameters.HubCount * RideParameters.GondolasPerHub;
    private const int TotalSeats = TotalGondolas * RideParameters.SeatsPerGondola;

    private static readonly Func<TimeSpan> NoDelay = () => TimeSpan.Zero;

    private static IReadOnlyList<PassengerWeight> Riders(int count, double kilograms = 75d) =>
        Enumerable.Range(0, count).Select(_ => new PassengerWeight(kilograms)).ToArray();

    private static Gondola GondolaAt(Ride ride, int hub, int gondola) =>
        ride.Mill.GetHub(hub).GetGondola(gondola);

    // The free-seat measure.

    [Fact]
    public void FreeSeatCount_OnAnEmptyRide_IsEverySeat()
    {
        var ride = Ride.Create();

        Assert.Equal(TotalSeats, ride.FreeSeatCount);
        Assert.Equal(TotalSeats, ride.Mill.FreeSeatCount);
        Assert.Equal(TotalSeats, ride.GroupBoardableSeats);
    }

    [Fact]
    public void FreeSeatCount_CountsTheSeatBesideALoneRider_WhileGroupCapacityDoesNot()
    {
        var ride = Ride.Create();

        // A group of three takes two gondolas and strands the seat beside its odd member.
        ride.BoardGroup(Riders(3), NoDelay);

        Assert.Equal(TotalSeats - 3, ride.FreeSeatCount);
        Assert.Equal(TotalGondolas - 2, ride.EmptyGondolaCount);
        Assert.Equal((TotalGondolas - 2) * RideParameters.SeatsPerGondola, ride.GroupBoardableSeats);
    }

    // Stranger pairing.

    [Fact]
    public void SeatSingleRiders_FillsTheSeatBesideAnEarlierGroupsLoneMember()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(1, kilograms: 60d), NoDelay); // sits alone in hub 0, gondola 0
        var gondola = GondolaAt(ride, 0, 0);
        Assert.Equal(1, gondola.OccupiedSeatCount);

        ride.SeatSingleRiders(Riders(1, kilograms: 90d), NoDelay);

        Assert.Equal(2, gondola.OccupiedSeatCount);
        Assert.Equal(60d, gondola.GetSeat(SeatPosition.Left).OccupiedKg);
        Assert.Equal(90d, gondola.GetSeat(SeatPosition.Right).OccupiedKg);
        Assert.Equal(TotalGondolas - 1, ride.EmptyGondolaCount);
    }

    [Fact]
    public void SeatSingleRiders_TwoSingles_ShareAnEmptyGondola()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders((TotalGondolas - 1) * 2), NoDelay); // one empty gondola remains
        Assert.Equal(1, ride.EmptyGondolaCount);

        ride.SeatSingleRiders(Riders(2), NoDelay);

        Assert.Equal(0, ride.EmptyGondolaCount);
        Assert.Equal(0, ride.FreeSeatCount);
        Assert.Equal(2, GondolaAt(ride, RideParameters.HubCount - 1, RideParameters.GondolasPerHub - 1).OccupiedSeatCount);
    }

    [Fact]
    public void SeatSingleRiders_CountsEveryRiderTowardThePassengerLoad()
    {
        var ride = Ride.Create();
        var riders = new[] { new PassengerWeight(60d), new PassengerWeight(70d), new PassengerWeight(80d) };

        ride.SeatSingleRiders(riders, NoDelay);

        Assert.Equal(210d, ride.Mill.PassengerLoadKg);
        Assert.Equal(3, ride.Mill.BoardedPassengerCount);
    }

    [Fact]
    public void SeatSingleRiders_CanFillEveryRemainingSeat()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(3), NoDelay);

        ride.SeatSingleRiders(Riders(TotalSeats - 3), NoDelay);

        Assert.Equal(0, ride.FreeSeatCount);
        Assert.Equal(TotalSeats, ride.Mill.BoardedPassengerCount);
    }

    [Fact]
    public void SeatSingleRiders_SchedulesEachRidersRestraintFromTheDelaySource()
    {
        var ride = Ride.Create();
        var delays = new Queue<TimeSpan>([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5)]);

        ride.SeatSingleRiders(Riders(2), delays.Dequeue);
        var gondola = GondolaAt(ride, 0, 0);

        // After two seconds only the first rider (one-second delay) has secured.
        ride.Advance(TimeSpan.FromSeconds(2));
        Assert.True(gondola.GetSeat(SeatPosition.Left).IsSecured);
        Assert.False(gondola.GetSeat(SeatPosition.Right).IsSecured);

        ride.Advance(TimeSpan.FromSeconds(4));
        Assert.True(gondola.GetSeat(SeatPosition.Right).IsSecured);
    }

    // Groups keep the old rule.

    [Fact]
    public void BoardGroup_IsStillRefusedAPartlyOccupiedGondola()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders((TotalGondolas - 1) * 2), NoDelay); // one empty gondola remains
        ride.SeatSingleRiders(Riders(1), NoDelay);                 // a single rider takes one of its seats
        Assert.Equal(1, ride.FreeSeatCount);
        Assert.Equal(0, ride.EmptyGondolaCount);
        var loadBefore = ride.Mill.PassengerLoadKg;

        // One seat is free, but a group — even a group of one — needs a whole empty gondola.
        Assert.Throws<DomainValidationException>(() => ride.BoardGroup(Riders(2), NoDelay));
        Assert.Throws<DomainValidationException>(() => ride.BoardGroup(Riders(1), NoDelay));

        Assert.Equal(1, ride.FreeSeatCount);
        Assert.Equal(loadBefore, ride.Mill.PassengerLoadKg);
    }

    [Fact]
    public void BoardGroup_AfterSingles_StillTakesOnlyWholeEmptyGondolas()
    {
        var ride = Ride.Create();
        ride.SeatSingleRiders(Riders(1), NoDelay); // hub 0, gondola 0, left seat

        ride.BoardGroup(Riders(2), NoDelay);

        // The pair did not split to sit beside the single rider.
        Assert.Equal(1, GondolaAt(ride, 0, 0).OccupiedSeatCount);
        Assert.Equal(2, GondolaAt(ride, 0, 1).OccupiedSeatCount);
    }

    // Capacity guard.

    [Fact]
    public void SeatSingleRiders_MoreRidersThanFreeSeats_Throws_AndSeatsNobody()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(TotalSeats - 2), NoDelay);
        Assert.Equal(2, ride.FreeSeatCount);

        var exception = Assert.Throws<DomainValidationException>(
            () => ride.SeatSingleRiders(Riders(3), NoDelay));

        Assert.Contains("only 2 seat(s) are free", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, ride.FreeSeatCount);
    }

    [Fact]
    public void SeatSingleRiders_OnAFullRide_Throws()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(TotalSeats), NoDelay);

        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders(Riders(1), NoDelay));
    }

    [Fact]
    public void SeatSingleRiders_WithNoRiders_Throws()
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders([], NoDelay));
        Assert.Equal(RideState.Idle, ride.CurrentState);
    }

    [Fact]
    public void SeatSingleRiders_NullArguments_Throw()
    {
        var ride = Ride.Create();

        Assert.Throws<ArgumentNullException>(() => ride.SeatSingleRiders(null!, NoDelay));
        Assert.Throws<ArgumentNullException>(() => ride.SeatSingleRiders(Riders(1), null!));
    }

    // Seat selection.

    [Fact]
    public void SeatSingleRiders_WithoutASelector_FillsFreeSeatsInNaturalOrder()
    {
        var ride = Ride.Create();

        ride.SeatSingleRiders(Riders(3), NoDelay);

        Assert.Equal(2, GondolaAt(ride, 0, 0).OccupiedSeatCount);
        Assert.True(GondolaAt(ride, 0, 1).GetSeat(SeatPosition.Left).IsOccupied);
        Assert.False(GondolaAt(ride, 0, 1).GetSeat(SeatPosition.Right).IsOccupied);
    }

    [Fact]
    public void SeatSingleRiders_HonoursTheSeatSelection_RiderByRider()
    {
        var ride = Ride.Create();
        var riders = new[] { new PassengerWeight(60d), new PassengerWeight(90d) };
        var asked = (Total: 0, Count: 0);

        // On a fresh ride free seat 5 is hub 0, gondola 2, right; free seat 0 is hub 0,
        // gondola 0, left. The first rider takes the first pick.
        ride.SeatSingleRiders(riders, NoDelay, selectSeats: (total, count) =>
        {
            asked = (total, count);
            return [5, 0];
        });

        Assert.Equal((TotalSeats, 2), asked);
        Assert.Equal(60d, GondolaAt(ride, 0, 2).GetSeat(SeatPosition.Right).OccupiedKg);
        Assert.Equal(90d, GondolaAt(ride, 0, 0).GetSeat(SeatPosition.Left).OccupiedKg);
        Assert.Equal(2, ride.Mill.BoardedPassengerCount);
    }

    [Fact]
    public void SeatSingleRiders_RandomSelection_SeatsEveryRiderInADistinctFreeSeat()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(7), NoDelay); // 25 seats free, one of them beside a lone rider
        var sampler = new RandomRideEventSampler(seed: 11);

        ride.SeatSingleRiders(Riders(10), NoDelay, sampler.NextGondolaSelection);

        Assert.Equal(17, ride.Mill.BoardedPassengerCount);
        Assert.Equal(TotalSeats - 17, ride.FreeSeatCount);
    }

    [Theory]
    [InlineData(new[] { 0 })]          // too few seats for two riders
    [InlineData(new[] { 0, 1, 2 })]    // too many
    [InlineData(new[] { 3, 3 })]       // the same seat twice
    [InlineData(new[] { 0, -1 })]      // below the free-seat list
    [InlineData(new[] { 0, 32 })]      // beyond the free-seat list
    public void SeatSingleRiders_WithAnInvalidSeatSelection_Throws_AndSeatsNobody(int[] picks)
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(
            () => ride.SeatSingleRiders(Riders(2), NoDelay, selectSeats: (_, _) => picks));

        Assert.True(ride.Mill.IsEmpty);
        Assert.Equal(RideState.Idle, ride.CurrentState);
    }

    [Fact]
    public void SeatSingleRiders_WithANegativeRestraintDelay_Throws_AndSeatsNobody()
    {
        var ride = Ride.Create();
        var delays = new Queue<TimeSpan>([TimeSpan.Zero, TimeSpan.FromSeconds(-1)]);

        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders(Riders(2), delays.Dequeue));

        // The first rider's delay was fine, but the call is all-or-nothing.
        Assert.True(ride.Mill.IsEmpty);
    }

    [Fact]
    public void SeatSingleRiders_WithANullRider_Throws_AndSeatsNobody()
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(
            () => ride.SeatSingleRiders([new PassengerWeight(75d), null!], NoDelay));

        Assert.True(ride.Mill.IsEmpty);
    }

    // State gating.

    [Fact]
    public void SeatSingleRiders_FromIdle_MovesTheRideToLoading()
    {
        var ride = Ride.Create();
        Assert.Equal(RideState.Idle, ride.CurrentState);

        ride.SeatSingleRiders(Riders(1), NoDelay);

        Assert.Equal(RideState.Loading, ride.CurrentState);
    }

    [Theory]
    [InlineData(RideState.Safe)]
    [InlineData(RideState.Started)]
    public void SeatSingleRiders_FromANonLoadableState_Throws_AndSeatsNobody(RideState state)
    {
        var ride = Ride.Create();
        // An empty ride is safe, so it can be driven through Safe to Started.
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        if (state == RideState.Started)
        {
            ride.RequestTransition(RideState.Started);
        }

        var exception = Assert.Throws<DomainValidationException>(
            () => ride.SeatSingleRiders(Riders(1), NoDelay));

        Assert.Contains("idle or loading", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(ride.Mill.IsEmpty);
        Assert.Equal(state, ride.CurrentState);
    }
}
