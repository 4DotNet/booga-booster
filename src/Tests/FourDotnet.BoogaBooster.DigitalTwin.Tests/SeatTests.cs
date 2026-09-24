using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class SeatTests
{
    [Fact]
    public void Empty_seat_measures_no_weight_and_is_open()
    {
        var seat = new Seat(SeatPosition.Left);

        Assert.False(seat.IsOccupied);
        Assert.Equal(0d, seat.OccupiedKg);
        Assert.Equal(RestraintState.Open, seat.Restraint);
        Assert.False(seat.NeedsSecuring);
    }

    [Fact]
    public void Boarding_registers_the_load_and_needs_securing()
    {
        var seat = new Seat(SeatPosition.Right);
        seat.Board(Passenger.OfWeight(90), TimeSpan.FromSeconds(10));

        Assert.True(seat.IsOccupied);
        Assert.Equal(90d, seat.OccupiedKg);
        Assert.True(seat.NeedsSecuring);
    }

    [Fact]
    public void Boarding_exposes_the_occupant_and_unboarding_hands_them_back()
    {
        var seat = new Seat(SeatPosition.Left);
        var passenger = Passenger.OfWeight(85);
        Assert.Null(seat.Occupant);

        seat.Board(passenger, TimeSpan.Zero);
        Assert.Same(passenger, seat.Occupant);

        var left = seat.Unboard();

        Assert.Same(passenger, left);
        Assert.Null(seat.Occupant);
        Assert.False(seat.IsOccupied);
    }

    [Fact]
    public void Unboarding_an_empty_seat_returns_nobody()
    {
        var seat = new Seat(SeatPosition.Right);

        Assert.Null(seat.Unboard());
    }

    [Fact]
    public void Cannot_board_an_occupied_seat()
    {
        var seat = new Seat(SeatPosition.Left);
        seat.Board(Passenger.OfWeight(70), TimeSpan.Zero);

        Assert.Throws<DomainValidationException>(() => seat.Board(Passenger.OfWeight(70), TimeSpan.Zero));
    }

    [Fact]
    public void Passenger_naturally_secures_the_restraint_after_the_delay()
    {
        var seat = new Seat(SeatPosition.Left);
        seat.Board(Passenger.OfWeight(80), TimeSpan.FromSeconds(15));

        // Before the delay elapses, the restraint stays open.
        seat.AdvanceNaturalBehavior(TimeSpan.FromSeconds(10));
        Assert.Equal(RestraintState.Open, seat.Restraint);
        Assert.True(seat.NeedsSecuring);

        // Once the delay passes, the passenger pulls it down and it locks.
        seat.AdvanceNaturalBehavior(TimeSpan.FromSeconds(6));
        Assert.Equal(RestraintState.Secured, seat.Restraint);
        Assert.True(seat.IsSecured);
        Assert.False(seat.NeedsSecuring);
    }

    [Fact]
    public void Empty_seat_never_secures()
    {
        var seat = new Seat(SeatPosition.Right);
        seat.AdvanceNaturalBehavior(TimeSpan.FromSeconds(60));

        Assert.Equal(RestraintState.Open, seat.Restraint);
    }

    [Fact]
    public void Restraint_can_be_worked_manually_through_the_states()
    {
        var seat = new Seat(SeatPosition.Left);
        seat.Board(Passenger.OfWeight(70), TimeSpan.FromSeconds(30));

        seat.CloseRestraint();
        Assert.Equal(RestraintState.Closed, seat.Restraint);

        seat.SecureRestraint();
        Assert.Equal(RestraintState.Secured, seat.Restraint);

        seat.OpenRestraint();
        Assert.Equal(RestraintState.Open, seat.Restraint);
    }

    [Fact]
    public void Cannot_secure_before_closing()
    {
        var seat = new Seat(SeatPosition.Left);
        seat.Board(Passenger.OfWeight(70), TimeSpan.FromSeconds(30));

        Assert.Throws<DomainValidationException>(() => seat.SecureRestraint());
    }
}
