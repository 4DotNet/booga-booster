using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers <see cref="Ride.BoardGroup"/>: pair seating, the lone odd member, the
/// capacity guard (measured in whole empty gondolas), state gating, and the load
/// each boarded member contributes.
/// </summary>
public sealed class RideBoardGroupTests
{
    private const int TotalGondolas = RideParameters.HubCount * RideParameters.GondolasPerHub;

    private static readonly Func<TimeSpan> NoDelay = () => TimeSpan.Zero;

    private static IReadOnlyList<PassengerWeight> Group(int size, double kilograms = 75d) =>
        Enumerable.Range(0, size).Select(_ => new PassengerWeight(kilograms)).ToArray();

    private static int OccupiedSeats(Gondola gondola) =>
        (gondola.GetSeat(SeatPosition.Left).IsOccupied ? 1 : 0)
        + (gondola.GetSeat(SeatPosition.Right).IsOccupied ? 1 : 0);

    private static IReadOnlyList<Gondola> OccupiedGondolas(Ride ride) =>
        ride.Mill.Hubs.SelectMany(h => h.Gondolas).Where(g => !g.IsEmpty).ToArray();

    [Fact]
    public void BoardGroup_SeatsAPairInASingleGondola()
    {
        var ride = Ride.Create();

        ride.BoardGroup(Group(2), NoDelay);

        Assert.Equal(TotalGondolas - 1, ride.EmptyGondolaCount);
        var gondola = Assert.Single(OccupiedGondolas(ride));
        Assert.Equal(2, OccupiedSeats(gondola));
    }

    [Fact]
    public void BoardGroup_OddMember_RidesAloneInASecondGondola()
    {
        var ride = Ride.Create();

        ride.BoardGroup(Group(3), NoDelay);

        Assert.Equal(TotalGondolas - 2, ride.EmptyGondolaCount);
        var occupied = OccupiedGondolas(ride);
        Assert.Equal(2, occupied.Count);
        Assert.Equal(1, occupied.Count(g => OccupiedSeats(g) == 2));
        Assert.Equal(1, occupied.Count(g => OccupiedSeats(g) == 1));
    }

    [Fact]
    public void BoardGroup_CountsEveryMemberWeightTowardPassengerLoad()
    {
        var ride = Ride.Create();
        var members = new[] { new PassengerWeight(60d), new PassengerWeight(70d), new PassengerWeight(80d) };

        ride.BoardGroup(members, NoDelay);

        Assert.Equal(210d, ride.Mill.PassengerLoadKg);
    }

    [Fact]
    public void BoardGroup_FromIdle_MovesRideToLoading()
    {
        var ride = Ride.Create();
        Assert.Equal(RideState.Idle, ride.CurrentState);

        ride.BoardGroup(Group(1), NoDelay);

        Assert.Equal(RideState.Loading, ride.CurrentState);
    }

    [Fact]
    public void BoardGroup_LargerThanTotalCapacity_IsRejectedAndSeatsNoOne()
    {
        var ride = Ride.Create();
        var oversized = Group((TotalGondolas * 2) + 1);

        Assert.Throws<DomainValidationException>(() => ride.BoardGroup(oversized, NoDelay));

        Assert.Equal(TotalGondolas, ride.EmptyGondolaCount);
        Assert.True(ride.Mill.IsEmpty);
    }

    [Fact]
    public void BoardGroup_WhenTooFewEmptyGondolasRemain_IsRejectedButLoneRiderStillFits()
    {
        var ride = Ride.Create();
        // A group of 30 seats two per gondola, consuming 15 of the 16 gondolas.
        ride.BoardGroup(Group(30), NoDelay);
        Assert.Equal(1, ride.EmptyGondolaCount);
        var loadBefore = ride.Mill.PassengerLoadKg;

        // A group of 3 needs two empty gondolas but only one remains.
        Assert.Throws<DomainValidationException>(() => ride.BoardGroup(Group(3), NoDelay));
        Assert.Equal(1, ride.EmptyGondolaCount);
        Assert.Equal(loadBefore, ride.Mill.PassengerLoadKg);

        // A lone rider needs only the one remaining gondola.
        ride.BoardGroup(Group(1), NoDelay);
        Assert.Equal(0, ride.EmptyGondolaCount);
    }

    [Fact]
    public void BoardGroup_WhenRideIsRunning_IsRejected()
    {
        var ride = Ride.Create();
        // An empty ride is safe, so it can be driven all the way to Started.
        ride.RequestTransition(RideState.Loading);
        ride.RequestTransition(RideState.Safe);
        ride.RequestTransition(RideState.Started);

        Assert.Throws<DomainValidationException>(() => ride.BoardGroup(Group(2), NoDelay));
        Assert.True(ride.Mill.IsEmpty);
    }

    [Fact]
    public void BoardGroup_EmptyGroup_IsRejected()
    {
        var ride = Ride.Create();

        Assert.Throws<DomainValidationException>(() => ride.BoardGroup([], NoDelay));
    }

    [Fact]
    public void BoardGroup_SeatsIntoTheEmptyGondolasChosenBySelector()
    {
        var ride = Ride.Create();
        // The selector picks position 2 in the empty-gondola list; on a fresh ride the
        // empty list is every gondola in natural order, so that is hub 0, gondola 2.
        ride.BoardGroup(Group(2), NoDelay, selectGondolas: (_, _) => [2]);

        var gondola = Assert.Single(OccupiedGondolas(ride));
        Assert.Equal(0, gondola.HubIndex);
        Assert.Equal(2, gondola.Index);
        Assert.Equal(2, OccupiedSeats(gondola));
    }

    [Fact]
    public void BoardGroup_RandomSelection_StillHonoursCapacityAndPairing()
    {
        var ride = Ride.Create();
        var sampler = new RandomRideEventSampler(seed: 3);

        // A group of 4 into a ride with exactly two empty gondolas: seat 15 pairs first,
        // leaving one empty gondola into which the random selection must still fit the group.
        ride.BoardGroup(Group(28), NoDelay); // fills 14 gondolas
        Assert.Equal(2, ride.EmptyGondolaCount);

        ride.BoardGroup(Group(4), NoDelay, sampler.NextGondolaSelection);

        Assert.Equal(0, ride.EmptyGondolaCount);
        var lastTwo = OccupiedGondolas(ride).Where(g => OccupiedSeats(g) == 2).ToArray();
        Assert.Equal(TotalGondolas, lastTwo.Length); // every gondola now holds a pair
    }
}
