using Bogus;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the loading algorithm in <see cref="RideLoadingCoordinator"/>: front-of-line
/// drain, look-ahead/backfill across the first three groups, the "full" stop
/// condition, mid-loading arrivals, and state gating. The ride is a real
/// <see cref="RideStore"/> so the empty-gondola capacity is exercised end to end; the
/// queue is an in-memory fake so groups can be arranged and observed by position.
/// </summary>
public sealed class RideLoadingCoordinatorTests
{
    private const int TotalGondolas = RideParameters.HubCount * RideParameters.GondolasPerHub;

    private static readonly Faker Faker = new() { Random = new Randomizer(8675309) };
    private static long _nextPersonNumber = 1;

    private static RideStore NewStore() => new(new RandomRideEventSampler(seed: 1));

    private static RideLoadingCoordinator NewCoordinator(RideStore store, IRideQueueService queue) =>
        new(store, NullLogger<RideLoadingCoordinator>.Instance, queue);

    /// <summary>A store already in <see cref="RideState.Loading"/> with all gondolas free.</summary>
    private static (RideLoadingCoordinator Coordinator, RideStore Store, FakeRideQueueService Queue, Guid RideId) Loading()
    {
        var store = NewStore();
        store.RequestStateTransition(RideState.Loading);
        var queue = new FakeRideQueueService();
        return (NewCoordinator(store, queue), store, queue, Guid.NewGuid());
    }

    private static QueuedGroupDto MakeGroup(int size)
    {
        var people = Enumerable.Range(0, size)
            .Select(_ => new PersonDto(_nextPersonNumber++, Faker.Name.FullName(), Faker.Random.Int(30, 150)))
            .ToArray();
        return new QueuedGroupDto(Guid.NewGuid(), people);
    }

    /// <summary>Fills gondolas (two riders each) until exactly <paramref name="desiredEmpty"/> remain free.</summary>
    private static void LeaveEmptyGondolas(RideStore store, int desiredEmpty)
    {
        var seats = (TotalGondolas - desiredEmpty) * RideParameters.SeatsPerGondola;
        var members = Enumerable.Range(0, seats).Select(_ => new PassengerWeight(75d)).ToArray();
        store.BoardGroup(members);
        Assert.Equal(desiredEmpty, store.EmptyGondolaCount);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // 4.1 — Fit + drain.

    [Fact]
    public async Task RunLoadingPass_BoardsEveryFittingGroup_AndEmptiesTheQueue()
    {
        var (coordinator, store, queue, rideId) = Loading();
        queue.Enqueue(MakeGroup(2));
        queue.Enqueue(MakeGroup(2));
        queue.Enqueue(MakeGroup(2));

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Empty(queue.Groups);
        Assert.Equal(TotalGondolas - 3, store.EmptyGondolaCount);
    }

    [Fact]
    public async Task RunLoadingPass_AdvancesTheFrontOfTheLine()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 1); // Room for exactly one more group.
        var first = queue.Enqueue(MakeGroup(2));
        var second = queue.Enqueue(MakeGroup(2));

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        // The front group boards and the one behind it becomes the new front.
        Assert.DoesNotContain(queue.Groups, g => g.GroupId == first.GroupId);
        var front = Assert.Single(queue.Groups);
        Assert.Equal(second.GroupId, front.GroupId);
    }

    // 4.2 — Look-ahead / backfill.

    [Fact]
    public async Task RunLoadingPass_SkipsTooLargeFront_ToBoardTheSecondGroup()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 2);
        var big = queue.Enqueue(MakeGroup(6));   // needs 3 gondolas — does not fit
        var mid = queue.Enqueue(MakeGroup(3));   // needs 2 — fits
        var tail = queue.Enqueue(MakeGroup(9));

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.DoesNotContain(queue.Groups, g => g.GroupId == mid.GroupId);
        Assert.Equal(new[] { big.GroupId, tail.GroupId }, queue.Groups.Select(g => g.GroupId));
        Assert.Equal(0, store.EmptyGondolaCount);
    }

    [Fact]
    public async Task RunLoadingPass_ReachesTheThirdGroup_WhenTheFirstTwoDoNotFit()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 2);
        var big1 = queue.Enqueue(MakeGroup(6)); // needs 3 — no
        var big2 = queue.Enqueue(MakeGroup(6)); // needs 3 — no
        var small = queue.Enqueue(MakeGroup(2)); // needs 1 — yes

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.DoesNotContain(queue.Groups, g => g.GroupId == small.GroupId);
        Assert.Equal(new[] { big1.GroupId, big2.GroupId }, queue.Groups.Select(g => g.GroupId));
    }

    [Fact]
    public async Task RunLoadingPass_KeepsBackfilling_WhileCapacityAndFittingGroupsRemain()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 3);
        var big = queue.Enqueue(MakeGroup(8)); // needs 4 — never fits the 3 free gondolas
        queue.Enqueue(MakeGroup(2));
        queue.Enqueue(MakeGroup(2));
        queue.Enqueue(MakeGroup(2));

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        // All three small groups backfill (the window slides forward as each boards);
        // only the too-large front group is left.
        var remaining = Assert.Single(queue.Groups);
        Assert.Equal(big.GroupId, remaining.GroupId);
        Assert.Equal(0, store.EmptyGondolaCount);
    }

    // 4.3 — Full.

    [Fact]
    public async Task RunLoadingPass_WhenNoneOfTheFirstThreeFit_TakesNothing()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 2);
        queue.Enqueue(MakeGroup(6));
        queue.Enqueue(MakeGroup(6));
        queue.Enqueue(MakeGroup(6));

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Equal(3, queue.Groups.Count);
        Assert.Equal(2, store.EmptyGondolaCount);
    }

    [Fact]
    public async Task RunLoadingPass_WithNoEmptyGondola_IsImmediatelyFull()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 0);
        queue.Enqueue(MakeGroup(2));

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Single(queue.Groups);
        Assert.Equal(0, store.EmptyGondolaCount);
    }

    [Fact]
    public async Task RunLoadingPass_DoesNotReachAFittingFourthGroup()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 2);
        queue.Enqueue(MakeGroup(6));
        queue.Enqueue(MakeGroup(6));
        queue.Enqueue(MakeGroup(6));
        var fourth = queue.Enqueue(MakeGroup(2)); // would fit, but is beyond the window

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Contains(queue.Groups, g => g.GroupId == fourth.GroupId);
        Assert.Equal(4, queue.Groups.Count);
    }

    // 4.4 — Mid-loading arrivals.

    [Fact]
    public async Task RunLoadingPass_BoardsAGroupThatArrivesDuringLoading()
    {
        var (coordinator, store, queue, rideId) = Loading();

        // First pass with an empty line boards no one.
        await coordinator.RunLoadingPassAsync(rideId, Ct);
        Assert.Equal(TotalGondolas, store.EmptyGondolaCount);

        // A group joins mid-loading; the next pass boards it on the spot.
        queue.Enqueue(MakeGroup(2));
        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Empty(queue.Groups);
        Assert.Equal(TotalGondolas - 1, store.EmptyGondolaCount);
    }

    [Fact]
    public async Task RunLoadingPass_LeavesAnOversizedArrivalQueued_WhileFull()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 0);

        queue.Enqueue(MakeGroup(2));
        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Single(queue.Groups);
        Assert.Equal(0, store.EmptyGondolaCount);
    }

    [Fact]
    public async Task RunLoadingPass_DoesNotBoardAGroupThatArrivesAfterLoadingEnds()
    {
        var (coordinator, store, queue, rideId) = Loading();
        store.RequestStateTransition(RideState.Safe); // empty ride is safe, so this is legal

        queue.Enqueue(MakeGroup(2));
        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Single(queue.Groups);
        Assert.Equal(TotalGondolas, store.EmptyGondolaCount);
    }

    // 4.5 — State gating.

    [Theory]
    [InlineData(false)] // Idle
    [InlineData(true)]  // Started
    public async Task RunLoadingPass_DoesNothing_WhenTheRideIsNotLoading(bool started)
    {
        var store = NewStore();
        if (started)
        {
            store.RequestStateTransition(RideState.Loading);
            store.RequestStateTransition(RideState.Safe);
            store.RequestStateTransition(RideState.Started);
        }

        var queue = new FakeRideQueueService();
        queue.Enqueue(MakeGroup(2));
        var coordinator = NewCoordinator(store, queue);

        await coordinator.RunLoadingPassAsync(Guid.NewGuid(), Ct);

        Assert.Single(queue.Groups);
    }

    [Fact]
    public async Task RunLoadingPass_WithAnEmptyQueue_SeatsNoOne()
    {
        var (coordinator, store, _, rideId) = Loading();

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Equal(TotalGondolas, store.EmptyGondolaCount);
        Assert.Equal(RideState.Loading, store.CurrentState);
    }

    /// <summary>
    /// In-memory <see cref="IRideQueueService"/> for the coordinator tests: an ordered
    /// list of groups that supports the status snapshot and take-by-id the coordinator
    /// uses. Enqueue only exists to arrange the line; the real service is covered by
    /// the Queue module's own tests.
    /// </summary>
    private sealed class FakeRideQueueService : IRideQueueService
    {
        private readonly List<QueuedGroupDto> _groups = [];

        public IReadOnlyList<QueuedGroupDto> Groups => _groups;

        public QueuedGroupDto Enqueue(QueuedGroupDto group)
        {
            _groups.Add(group);
            return group;
        }

        public Task<QueuedGroupDto> EnqueueGroupAsync(Guid rideId, int groupSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public QueueStatusDto GetStatus(Guid rideId) =>
            new(rideId, _groups.Count, _groups.Sum(g => g.Size), [.. _groups]);

        public Task<QueuedGroupDto?> TakeGroupAsync(Guid rideId, Guid groupId, CancellationToken cancellationToken)
        {
            var index = _groups.FindIndex(g => g.GroupId == groupId);
            if (index < 0)
            {
                return Task.FromResult<QueuedGroupDto?>(null);
            }

            var group = _groups[index];
            _groups.RemoveAt(index);
            return Task.FromResult<QueuedGroupDto?>(group);
        }
    }
}
