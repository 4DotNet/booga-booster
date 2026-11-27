using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers <see cref="Ride.SeatSingleRiders"/> and the free-seat measure behind it: a
/// single rider may sit beside a stranger, groups still take only empty gondolas,
/// the capacity guard counts seats rather than gondolas, seat selection is honoured,
/// and state gating matches group boarding.
/// </summary>
public sealed class RideSeatSingleRidersTests
{
    private const int TotalGondolas = RideParameters.HubCount * RideParameters.GondolasPerHub;
    private const int TotalSeats = TotalGondolas * RideParameters.SeatsPerGondola;

    private static readonly Func<TimeSpan> NoDelay = () => TimeSpan.Zero;

    private static IReadOnlyList<PassengerWeight> Riders(int count, double kilograms = 75d) =>
        Enumerable.Range(0, count).Select(_ => new PassengerWeight(kilograms)).ToArray();

    private static IEnumerable<Gondola> AllGondolas(Ride ride) => ride.Mill.Hubs.SelectMany(h => h.Gondolas);

    [Fact]
    public void FreeSeatCount_OnAnEmptyRide_IsEverySeat()
    {
        var ride = Ride.Create();

        Assert.Equal(TotalSeats, ride.FreeSeatCount);
        Assert.Equal(TotalSeats, ride.GroupBoardableSeats);
        Assert.Equal(TotalSeats, ride.Mill.FreeSeats().Count());
    }

    [Fact]
    public void FreeSeatCount_CountsTheSeatBesideALoneRider_GroupBoardableSeatsDoesNot()
    {
        var ride = Ride.Create();

        ride.BoardGroup(Riders(3), NoDelay); // one full pair, one lone rider

        Assert.Equal(TotalSeats - 3, ride.FreeSeatCount);
        Assert.Equal((TotalGondolas - 2) * RideParameters.SeatsPerGondola, ride.GroupBoardableSeats);
    }

    [Fact]
    public void FreeSeats_AreInStableHubGondolaSeatOrder()
    {
        var ride = Ride.Create();

        var first = ride.Mill.FreeSeats().Take(3).ToArray();

        Assert.Equal((0, 0, SeatPosition.Left), (first[0].Gondola.HubIndex, first[0].Gondola.Index, first[0].Seat));
        Assert.Equal((0, 0, SeatPosition.Right), (first[1].Gondola.HubIndex, first[1].Gondola.Index, first[1].Seat));
        Assert.Equal((0, 1, SeatPosition.Left), (first[2].Gondola.HubIndex, first[2].Gondola.Index, first[2].Seat));
    }

    [Fact]
    public void SeatSingleRiders_FillsTheSeatBesideAnEarlierGroupsLoneMember()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(TotalSeats - 1), NoDelay); // every gondola full but the last, which holds one
        Assert.Equal(0, ride.EmptyGondolaCount);
        Assert.Equal(1, ride.FreeSeatCount);

        ride.SeatSingleRiders(Riders(1), NoDelay);

        Assert.Equal(0, ride.FreeSeatCount);
        Assert.All(AllGondolas(ride), g => Assert.Equal(2, g.OccupiedSeatCount));
    }

    [Fact]
    public void SeatSingleRiders_TwoSinglesShareAnEmptyGondola()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(TotalSeats - 2), NoDelay); // exactly one empty gondola left
        Assert.Equal(1, ride.EmptyGondolaCount);

        ride.SeatSingleRiders(Riders(2), NoDelay);

        Assert.Equal(0, ride.EmptyGondolaCount);
        Assert.Equal(0, ride.FreeSeatCount);
    }

    [Fact]
    public void BoardGroup_IsStillRefusedAPartlyOccupiedGondola()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(TotalSeats - 2), NoDelay);
        ride.SeatSingleRiders(Riders(1), NoDelay); // a single rider alone in the last gondola
        Assert.Equal(0, ride.EmptyGondolaCount);
        Assert.Equal(1, ride.FreeSeatCount);

        Assert.Throws<DomainValidationException>(() => ride.BoardGroup(Riders(2), NoDelay));
        Assert.Throws<DomainValidationException>(() => ride.BoardGroup(Riders(1), NoDelay));
        Assert.Equal(1, ride.FreeSeatCount);
    }

    [Fact]
    public void SeatSingleRiders_MoreRidersThanFreeSeats_ThrowsAndSeatsNobody()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Riders(TotalSeats - 2), NoDelay);
        var loadBefore = ride.Mill.PassengerLoadKg;

        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders(Riders(3), NoDelay));

        Assert.Equal(2, ride.FreeSeatCount);
        Assert.Equal(loadBefore, ride.Mill.PassengerLoadKg);
    }

    [Fact]
    public void SeatSingleRiders_HonoursTheSeatSelection()
    {
        var ride = Ride.Create();

        // Free-seat list on a fresh ride: [h0g0L, h0g0R, h0g1L, h0g1R, ...]; index 3 is h0g1R.
        ride.SeatSingleRiders(Riders(1), NoDelay, selectSeats: (_, _) => [3]);

        var gondola = Assert.Single(AllGondolas(ride), g => !g.IsEmpty);
        Assert.Equal(0, gondola.HubIndex);
        Assert.Equal(1, gondola.Index);
        Assert.True(gondola.GetSeat(SeatPosition.Right).IsOccupied);
        Assert.False(gondola.GetSeat(SeatPosition.Left).IsOccupied);
    }

    [Fact]
    public void SeatSingleRiders_DefaultSelection_FillsSeatsInNaturalOrder()
    {
        var ride = Ride.Create();

        ride.SeatSingleRiders(Riders(3), NoDelay);

        var occupied = AllGondolas(ride).Where(g => !g.IsEmpty).ToArray();
        Assert.Equal(2, occupied.Length);
        Assert.Equal(2, occupied[0].OccupiedSeatCount);
        Assert.True(occupied[1].GetSeat(SeatPosition.Left).IsOccupied);
    }

    [Fact]
    public void SeatSingleRiders_RandomSelection_FillsEveryRemainingSeat()
    {
        var ride = Ride.Create();
        var sampler = new RandomRideEventSampler(seed: 5);
        ride.BoardGroup(Riders(7), NoDelay); // 4 gondolas, one with a lone rider

        ride.SeatSingleRiders(Riders(ride.FreeSeatCount), NoDelay, sampler.NextGondolaSelection);

        Assert.Equal(0, ride.FreeSeatCount);
    }

    [Theory]
    [InlineData(new[] { 0, 0 })]   // duplicate
    [InlineData(new[] { 0 })]      // too few
    [InlineData(new[] { 0, 99 })]  // out of range
    [InlineData(new[] { -1, 0 })]  // negative
    public void SeatSingleRiders_InvalidSelection_ThrowsAndSeatsNobody(int[] picks)
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(
            () => ride.SeatSingleRiders(Riders(2), NoDelay, selectSeats: (_, _) => picks));

        Assert.True(ride.Mill.IsEmpty);
    }

    [Fact]
    public void SeatSingleRiders_MovesAnIdleRideIntoLoading()
    {
        var ride = Ride.Create();

        ride.SeatSingleRiders(Riders(1), NoDelay);

        Assert.Equal(RideState.Loading, ride.CurrentState);
    }

    [Fact]
    public void SeatSingleRiders_FromANonLoadableState_IsRejected()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);

        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders(Riders(1), NoDelay));

        ride.RequestTransition(RideState.Started);
        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders(Riders(1), NoDelay));
        Assert.True(ride.Mill.IsEmpty);
    }

    [Fact]
    public void SeatSingleRiders_EmptyList_IsRejected()
    {
        Assert.Throws<DomainValidationException>(() => Ride.Create().SeatSingleRiders([], NoDelay));
    }

    [Fact]
    public void SeatSingleRiders_NullArguments_Throw()
    {
        var ride = Ride.Create();

        Assert.Throws<ArgumentNullException>(() => ride.SeatSingleRiders(null!, NoDelay));
        Assert.Throws<ArgumentNullException>(() => ride.SeatSingleRiders(Riders(1), null!));
    }

    [Fact]
    public void SeatSingleRiders_DefaultPlacement_FillsTheSeatBesideALoneRider_BeforeAnEmptyGondola()
    {
        var ride = Ride.Create();
        // A lone group member in hub 2, gondola 1 (index 9 in the empty-gondola list).
        ride.BoardGroup(Riders(1), NoDelay, selectGondolas: (_, _) => [9]);
        Assert.Equal(TotalGondolas - 1, ride.EmptyGondolaCount);

        ride.SeatSingleRiders(Riders(1), NoDelay);

        var shared = Assert.Single(AllGondolas(ride), g => !g.IsEmpty);
        Assert.Equal((2, 1), (shared.HubIndex, shared.Index));
        Assert.Equal(2, shared.OccupiedSeatCount);
        Assert.Equal(TotalGondolas - 1, ride.EmptyGondolaCount); // no gondola taken from groups
    }

    [Fact]
    public void SeatSingleRiders_DefaultPlacement_PairsSinglesRatherThanSpreadingThem()
    {
        var ride = Ride.Create();

        ride.SeatSingleRiders(Riders(4), NoDelay);

        Assert.Equal(TotalGondolas - 2, ride.EmptyGondolaCount);
        Assert.All(AllGondolas(ride).Where(g => !g.IsEmpty), g => Assert.Equal(2, g.OccupiedSeatCount));
    }

    [Fact]
    public void RideStore_SeatSingleRiders_UsesTheCapacitySavingPlacement()
    {
        // The production path: a real store with its random sampler. Whichever gondola
        // the random group selection put the lone rider in, the single sits beside them.
        var store = new RideStore(new RandomRideEventSampler(seed: 42));
        store.BoardGroup([new PassengerWeight(80d)]);
        Assert.Equal(TotalGondolas - 1, store.EmptyGondolaCount);

        store.SeatSingleRiders([new PassengerWeight(70d)]);

        Assert.Equal(TotalGondolas - 1, store.EmptyGondolaCount);
        Assert.Equal(TotalSeats - 2, store.FreeSeatCount);
    }

    [Fact]
    public void RideStore_SeatSingleRiders_SeatsUnderTheStoreAndReportsFreeSeats()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        store.BoardGroup(Riders(5).ToArray());
        Assert.Equal(TotalSeats - 5, store.FreeSeatCount);

        store.SeatSingleRiders(Riders(3).ToArray());

        Assert.Equal(TotalSeats - 8, store.FreeSeatCount);
        Assert.Equal(RideState.Loading, store.CurrentState);
    }
}
