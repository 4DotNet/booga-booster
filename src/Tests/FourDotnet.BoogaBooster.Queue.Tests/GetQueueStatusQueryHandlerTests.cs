using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using FourDotnet.BoogaBooster.Queue.Features.GetQueueStatus;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the GetQueueStatus feature handler: it reads the ride named by the
/// query through the module contract and returns that snapshot unchanged.
/// </summary>
public sealed class GetQueueStatusQueryHandlerTests
{
    private static GetQueueStatusResponse SnapshotFor(Guid rideId, int groupSize)
    {
        var people = Enumerable.Range(1, groupSize)
            .Select(n => new PersonDto(n, $"Person {n}", 80))
            .ToArray();
        var groups = new[] { new QueuedGroupDto(Guid.NewGuid(), people) };

        return new GetQueueStatusResponse(rideId, GroupCount: 1, PeopleWaiting: groupSize, Groups: groups);
    }

    [Fact]
    public async Task HandleAsync_ReturnsTheSnapshot_ForTheQueriedRide()
    {
        var rideId = Guid.NewGuid();
        var expected = SnapshotFor(rideId, groupSize: 3);
        var service = new Mock<IRideQueueService>();
        service.Setup(s => s.GetStatus(rideId)).Returns(expected);
        var handler = new GetQueueStatusQueryHandler(service.Object);

        var result = await handler.HandleAsync(
            new GetQueueStatusQuery(rideId),
            TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        service.Verify(s => s.GetStatus(rideId), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_ForRideWithNoQueue_ReturnsTheEmptySnapshot()
    {
        // The service reports an empty line for an unknown ride rather than failing,
        // and the handler passes that through untouched.
        var rideId = Guid.NewGuid();
        var empty = new GetQueueStatusResponse(rideId, GroupCount: 0, PeopleWaiting: 0, Groups: []);
        var service = new Mock<IRideQueueService>();
        service.Setup(s => s.GetStatus(rideId)).Returns(empty);
        var handler = new GetQueueStatusQueryHandler(service.Object);

        var result = await handler.HandleAsync(
            new GetQueueStatusQuery(rideId),
            TestContext.Current.CancellationToken);

        Assert.Equal(rideId, result.RideId);
        Assert.Empty(result.Groups);
        Assert.Equal(0, result.PeopleWaiting);
    }

    [Fact]
    public async Task HandleAsync_NullQuery_Throws()
    {
        var handler = new GetQueueStatusQueryHandler(new Mock<IRideQueueService>().Object);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.HandleAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_NullQueueService_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GetQueueStatusQueryHandler(null!));
    }
}
