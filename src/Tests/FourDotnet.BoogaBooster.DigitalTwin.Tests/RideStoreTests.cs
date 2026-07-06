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
