using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the queue → ride hand-off of guest experience in
/// <see cref="RideLoadingCoordinator"/>: the riders who board carry exactly the ratings
/// the Queue module reported when the group was taken — including the happiness the
/// wait already eroded (docs/06 §6.7).
/// </summary>
public sealed class RideLoadingCoordinatorExperienceTests
{
    [Fact]
    public async Task Boarded_riders_carry_the_ratings_of_the_taken_group()
    {
        var rideId = Guid.NewGuid();
        var store = new RideStore(new RandomRideEventSampler(seed: 3));
        store.RequestStateTransition(RideState.Loading);

        // The queue reports a group whose members' happiness has already eroded to 60 and 40.
        var group = new QueuedGroupDto(
            Guid.NewGuid(),
            [
                new PersonDto(1, "Ada", 70, Happiness: 60d, PreferredIntensity: 90d, Nausea: 0d),
                new PersonDto(2, "Bob", 80, Happiness: 40d, PreferredIntensity: 70d, Nausea: 10d),
            ]);
        var queue = new Mock<IRideQueueService>();
        queue
            .SetupSequence(q => q.GetStatus(rideId))
            .Returns(new GetQueueStatusResponse(rideId, 1, 2, [group], AverageHappiness: 50d))
            .Returns(new GetQueueStatusResponse(rideId, 0, 0, [], AverageHappiness: null));
        queue
            .Setup(q => q.TakeGroupAsync(rideId, group.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        var coordinator = new RideLoadingCoordinator(store, NullLogger<RideLoadingCoordinator>.Instance, queue.Object);

        await coordinator.RunLoadingPassAsync(rideId, TestContext.Current.CancellationToken);

        var telemetry = store.GetTelemetry();
        Assert.Equal(2, telemetry.BoardedPassengerCount);
        Assert.Equal(new RiderExperienceTelemetry(50d, 80d, 5d), telemetry.RiderExperience);
        Assert.Equal(150d, telemetry.Gondolas.Sum(g => g.LoadKg));
    }

    [Fact]
    public async Task A_single_boarded_rider_has_exactly_the_reported_happiness()
    {
        var rideId = Guid.NewGuid();
        var store = new RideStore(new RandomRideEventSampler(seed: 3));
        store.RequestStateTransition(RideState.Loading);
        var group = new QueuedGroupDto(
            Guid.NewGuid(),
            [new PersonDto(1, "Ada", 70, Happiness: 61.25d, PreferredIntensity: 55d, Nausea: 0d)]);
        var queue = new Mock<IRideQueueService>();
        queue
            .SetupSequence(q => q.GetStatus(rideId))
            .Returns(new GetQueueStatusResponse(rideId, 1, 1, [group], AverageHappiness: 61.25d))
            .Returns(new GetQueueStatusResponse(rideId, 0, 0, [], AverageHappiness: null));
        queue
            .Setup(q => q.TakeGroupAsync(rideId, group.GroupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);
        var coordinator = new RideLoadingCoordinator(store, NullLogger<RideLoadingCoordinator>.Instance, queue.Object);

        await coordinator.RunLoadingPassAsync(rideId, TestContext.Current.CancellationToken);

        Assert.Equal(61.25d, store.GetTelemetry().RiderExperience.AverageHappiness);
    }
}
