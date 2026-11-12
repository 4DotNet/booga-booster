using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the mill's seat-level capacity measure: <see cref="GreatMill.FreeSeatCount"/>
/// counts every unoccupied seat, and <see cref="GreatMill.FreeSeats"/> yields them in
/// the stable hub-then-gondola order — including the free seat of a partly-occupied
/// gondola, which <see cref="GreatMill.EmptyGondolaCount"/> deliberately ignores.
/// </summary>
public sealed class GreatMillFreeSeatsTests
{
    private const int TotalGondolas = RideParameters.HubCount * RideParameters.GondolasPerHub;
    private const int TotalSeats = TotalGondolas * RideParameters.SeatsPerGondola;

    [Fact]
    public void FreeSeatCount_OnAnEmptyMill_IsEverySeat()
    {
        Assert.Equal(TotalSeats, new GreatMill().FreeSeatCount);
    }

    [Fact]
    public void FreeSeatCount_CountsTheSeatBesideALoneRider_WhileEmptyGondolaCountDoesNot()
    {
        var mill = new GreatMill();

        mill.GetHub(1).GetGondola(2).Board(SeatPosition.Left, Passenger.OfWeight(75d), TimeSpan.Zero);

        Assert.Equal(TotalSeats - 1, mill.FreeSeatCount);
        Assert.Equal(TotalGondolas - 1, mill.EmptyGondolaCount);
    }

    [Fact]
    public void FreeSeatCount_OnAFullMill_IsZero()
    {
        var mill = new GreatMill();
        foreach (var hub in mill.Hubs)
        {
            TestHelpers.FillHub(hub, 75d);
        }

        Assert.Equal(0, mill.FreeSeatCount);
        Assert.Empty(mill.FreeSeats());
    }

    [Fact]
    public void FreeSeats_YieldsEverySeat_HubThenGondola_LeftBeforeRight()
    {
        var mill = new GreatMill();

        var seats = mill.FreeSeats().ToArray();

        var expected = (
            from hub in Enumerable.Range(0, RideParameters.HubCount)
            from gondola in Enumerable.Range(0, RideParameters.GondolasPerHub)
            from seat in new[] { SeatPosition.Left, SeatPosition.Right }
            select (hub, gondola, seat)).ToArray();
        Assert.Equal(expected, seats.Select(s => (s.Gondola.HubIndex, s.Gondola.Index, s.Seat)));
    }

    [Fact]
    public void FreeSeats_SkipsOccupiedSeats_AndKeepsTheFreeSeatOfAPartlyOccupiedGondola()
    {
        var mill = new GreatMill();
        mill.GetHub(0).GetGondola(0).Board(SeatPosition.Left, Passenger.OfWeight(75d), TimeSpan.Zero);
        mill.GetHub(0).GetGondola(1).Board(SeatPosition.Right, Passenger.OfWeight(75d), TimeSpan.Zero);

        var seats = mill.FreeSeats().Take(3).Select(s => (s.Gondola.Index, s.Seat)).ToArray();

        Assert.Equal([(0, SeatPosition.Right), (1, SeatPosition.Left), (2, SeatPosition.Left)], seats);
        Assert.Equal(mill.FreeSeatCount, mill.FreeSeats().Count());
    }
}
