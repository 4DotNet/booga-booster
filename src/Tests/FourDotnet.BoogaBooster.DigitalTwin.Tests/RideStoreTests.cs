using System.Linq;
using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class RideStoreTests
{
    [Fact]
    public void Boarding_without_a_weight_draws_a_valid_random_passenger()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 123));

        var telemetry = store.BoardPassenger(0, 0, SeatPosition.Left, weight: null);

        var seat = telemetry.Gondolas[0].Seats.Single(s => s.Position == SeatPosition.Left);
        Assert.InRange(seat.OccupiedKg, RideParameters.MinPassengerKg, RideParameters.MaxPassengerKg);
    }

    [Fact]
    public void Boarding_with_a_weight_registers_that_exact_load()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));

        var telemetry = store.BoardPassenger(1, 2, SeatPosition.Right, new PassengerWeight(88d));

        var gondola = telemetry.Gondolas.Single(g => g is { HubIndex: 1, Index: 2 });
        var seat = gondola.Seats.Single(s => s.Position == SeatPosition.Right);
        Assert.Equal(88d, seat.OccupiedKg);
    }

    [Fact]
    public void Advancing_moves_simulated_time_forward()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        var before = store.GetTelemetry().SimulationTimeSeconds;

        var after = store.Advance(TimeSpan.FromSeconds(0.5d)).SimulationTimeSeconds;

        Assert.True(after > before);
    }

    [Fact]
    public void A_seated_passenger_secures_within_the_maximum_delay()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 5));
        store.BoardPassenger(0, 0, SeatPosition.Left, new PassengerWeight(75d));

        // The maximum natural delay is 30 s; advance a little past it.
        var steps = (int)(31d / TestHelpers.Dt.TotalSeconds);
        for (var i = 0; i < steps; i++)
        {
            store.Advance(TestHelpers.Dt);
        }

        var seat = store.GetTelemetry().Gondolas[0].Seats.Single(s => s.Position == SeatPosition.Left);
        Assert.Equal(RestraintState.Secured, seat.Restraint);
    }

    [Fact]
    public void Starting_an_unsafe_ride_through_the_store_is_rejected()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        store.BoardPassenger(0, 0, SeatPosition.Left, new PassengerWeight(75d));

        Assert.Throws<DomainValidationException>(() => store.StartRide());
    }

    [Fact]
    public void Free_seat_count_includes_the_seat_beside_a_lone_rider()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        var totalSeats = RideParameters.HubCount * RideParameters.GondolasPerHub * RideParameters.SeatsPerGondola;
        Assert.Equal(totalSeats, store.FreeSeatCount);

        store.BoardGroup([new PassengerWeight(75d)]);

        // The lone rider's gondola is no longer empty, yet its other seat is still free.
        Assert.Equal(totalSeats - 1, store.FreeSeatCount);
        Assert.Equal((totalSeats / RideParameters.SeatsPerGondola) - 1, store.EmptyGondolaCount);
    }

    [Fact]
    public void Seating_single_riders_fills_distinct_free_seats_and_reports_the_load()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 5));
        var riders = new[] { new PassengerWeight(60d), new PassengerWeight(70d), new PassengerWeight(80d) };

        var telemetry = store.SeatSingleRiders(riders);

        Assert.Equal(3, telemetry.BoardedPassengerCount);
        Assert.Equal(RideState.Loading, telemetry.State);
        Assert.Equal(210d, telemetry.Mill.PassengerLoadKg);
        Assert.Equal(
            [60d, 70d, 80d],
            telemetry.Gondolas.SelectMany(g => g.Seats).Where(s => s.IsOccupied).Select(s => s.OccupiedKg).Order());
    }

    [Fact]
    public void Seating_single_riders_is_reproducible_from_the_sampler_seed()
    {
        static (int Hub, int Gondola, SeatPosition Seat)[] SeatsTaken(int seed)
        {
            var store = new RideStore(new RandomRideEventSampler(seed));
            var telemetry = store.SeatSingleRiders([new PassengerWeight(75d), new PassengerWeight(75d)]);
            return
            [
                .. from gondola in telemetry.Gondolas
                   from seat in gondola.Seats
                   where seat.IsOccupied
                   select (gondola.HubIndex, gondola.Index, seat.Position),
            ];
        }

        Assert.Equal(SeatsTaken(seed: 42), SeatsTaken(seed: 42));
    }

    [Fact]
    public void Seating_more_single_riders_than_free_seats_is_rejected_and_seats_nobody()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        var totalSeats = RideParameters.HubCount * RideParameters.GondolasPerHub * RideParameters.SeatsPerGondola;
        store.BoardGroup([.. Enumerable.Range(0, totalSeats - 1).Select(_ => new PassengerWeight(75d))]);
        Assert.Equal(1, store.FreeSeatCount);

        Assert.Throws<DomainValidationException>(
            () => store.SeatSingleRiders([new PassengerWeight(75d), new PassengerWeight(75d)]));

        Assert.Equal(1, store.FreeSeatCount);
    }

    [Fact]
    public void Seating_single_riders_requires_a_rider_list()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));

        Assert.Throws<ArgumentNullException>(() => store.SeatSingleRiders(null!));
    }

    [Fact]
    public void The_simulation_service_tick_advances_the_store()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        var logger = new Mock<ILogger<RideSimulationService>>().Object;
        var coordinator = new RideLoadingCoordinator(store, NullLogger<RideLoadingCoordinator>.Instance);
        var options = Options.Create(new DigitalTwinModuleOptions());
        var service = new RideSimulationService(store, coordinator, options, TimeProvider.System, logger);

        var before = store.GetTelemetry().SimulationTimeSeconds;
        service.Tick();
        var after = store.GetTelemetry().SimulationTimeSeconds;

        Assert.True(after > before);
    }
}
