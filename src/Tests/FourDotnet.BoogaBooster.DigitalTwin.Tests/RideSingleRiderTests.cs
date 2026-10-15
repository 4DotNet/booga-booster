using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers <see cref="Ride.SeatSingleRiders"/> and the free-seat measures behind it:
/// single riders may take any unoccupied seat (pairing with strangers) while groups
/// keep needing whole empty gondolas.
/// </summary>
public sealed class RideSingleRiderTests
{
    private const int TotalGondolas = RideParameters.HubCount * RideParameters.GondolasPerHub;
    private const int TotalSeats = TotalGondolas * RideParameters.SeatsPerGondola;

    private static readonly Func<TimeSpan> NoDelay = () => TimeSpan.Zero;

    private static IReadOnlyList<PassengerWeight> Weights(int count, double kilograms = 70d) =>
        Enumerable.Range(0, count).Select(_ => new PassengerWeight(kilograms)).ToArray();

    private static Gondola FirstGondola(Ride ride) => ride.Mill.Hubs[0].Gondolas[0];

    [Fact]
    public void SeatSingleRiders_FillsTheSeatBesideAnEarlierGroupsLoneMember()
    {
        var ride = Ride.Create();
        ride.BoardGroup(Weights(1), NoDelay); // a lone member takes the left seat of the first gondola

        ride.SeatSingleRiders(Weights(1), NoDelay);

        var gondola = FirstGondola(ride);
        Assert.True(gondola.GetSeat(SeatPosition.Left).IsOccupied);
        Assert.True(gondola.GetSeat(SeatPosition.Right).IsOccupied);
        Assert.Equal(TotalGondolas - 1, ride.EmptyGondolaCount);
    }

    [Fact]
    public void SeatSingleRiders_TwoSinglesShareAnEmptyGondola()
    {
        var ride = Ride.Create();

        ride.SeatSingleRiders(Weights(2), NoDelay);

        Assert.Equal(2, FirstGondola(ride).OccupiedSeatCount);
        Assert.Equal(TotalGondolas - 1, ride.EmptyGondolaCount);
    }

    [Fact]
    public void BoardGroup_IsStillRefusedAPartlyOccupiedGondola()
    {
        var ride = Ride.Create();
        // Leave exactly one seat free on the whole ride, beside a single rider.
        ride.SeatSingleRiders(Weights(TotalSeats - 1), NoDelay);
        Assert.Equal(1, ride.FreeSeatCount);
        Assert.Equal(0, ride.EmptyGondolaCount);
        Assert.Equal(0, ride.GroupBoardableSeats);

        Assert.Throws<DomainValidationException>(() => ride.BoardGroup(Weights(1), NoDelay));
    }

    [Fact]
    public void SeatSingleRiders_MoreRidersThanFreeSeats_ThrowsAndSeatsNoOne()
    {
        var ride = Ride.Create();
        ride.SeatSingleRiders(Weights(TotalSeats - 1), NoDelay);

        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders(Weights(2), NoDelay));

        Assert.Equal(1, ride.FreeSeatCount);
    }

    [Fact]
    public void SeatSingleRiders_HonoursTheSeatSelector()
    {
        var ride = Ride.Create();

        // Free seat list is gondola0-L, gondola0-R, gondola1-L, ...; pick index 3 = gondola 1, right.
        ride.SeatSingleRiders(Weights(1), NoDelay, selectSeats: (_, _) => [3]);

        var second = ride.Mill.Hubs[0].Gondolas[1];
        Assert.False(second.GetSeat(SeatPosition.Left).IsOccupied);
        Assert.True(second.GetSeat(SeatPosition.Right).IsOccupied);
        Assert.True(FirstGondola(ride).IsEmpty);
    }

    [Fact]
    public void SeatSingleRiders_RandomSelection_SeatsDistinctSeatsAndCountsLoad()
    {
        var ride = Ride.Create();
        var sampler = new RandomRideEventSampler(seed: 7);

        ride.SeatSingleRiders(Weights(10, 80d), NoDelay, sampler.NextGondolaSelection);

        Assert.Equal(10, ride.Mill.BoardedPassengerCount);
        Assert.Equal(TotalSeats - 10, ride.FreeSeatCount);
        Assert.Equal(800d, ride.Mill.PassengerLoadKg);
    }

    [Fact]
    public void SeatSingleRiders_FromIdle_MovesRideToLoading()
    {
        var ride = Ride.Create();

        ride.SeatSingleRiders(Weights(1), NoDelay);

        Assert.Equal(RideState.Loading, ride.CurrentState);
    }

    [Fact]
    public void SeatSingleRiders_WhenRideIsRunning_Throws()
    {
        var ride = Ride.Create();
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);

        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders(Weights(1), NoDelay));
        Assert.True(ride.Mill.IsEmpty);
    }

    [Fact]
    public void SeatSingleRiders_RejectsNoRidersAndNullArguments()
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(() => ride.SeatSingleRiders([], NoDelay));
        Assert.Throws<ArgumentNullException>(() => ride.SeatSingleRiders(null!, NoDelay));
        Assert.Throws<ArgumentNullException>(() => ride.SeatSingleRiders(Weights(1), null!));
    }

    [Fact]
    public void FreeSeats_ListsEveryUnoccupiedSeatInStableOrder()
    {
        var ride = Ride.Create();
        ride.SeatSingleRiders(Weights(1), NoDelay); // gondola 0, left

        var free = ride.Mill.FreeSeats().ToArray();

        Assert.Equal(TotalSeats - 1, free.Length);
        Assert.Equal(TotalSeats - 1, ride.Mill.FreeSeatCount);
        Assert.Same(FirstGondola(ride), free[0].Gondola);
        Assert.Equal(SeatPosition.Right, free[0].Seat);
    }

    [Fact]
    public void GroupBoardableSeats_StillCountsOnlyWholeEmptyGondolas()
    {
        var ride = Ride.Create();
        ride.SeatSingleRiders(Weights(1), NoDelay);

        Assert.Equal((TotalGondolas - 1) * RideParameters.SeatsPerGondola, ride.GroupBoardableSeats);
        Assert.Equal(TotalSeats - 1, ride.FreeSeatCount);
    }
}
