using Bogus;
using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

    // Single riders — the top-up phase.

    private static int TotalSeats => TotalGondolas * RideParameters.SeatsPerGondola;

    private static PersonDto MakeSingleRider() =>
        new(_nextPersonNumber++, Faker.Name.FullName(), Faker.Random.Int(30, 150));

    /// <summary>Puts <paramref name="count"/> individuals in the single-riders line and returns them in arrival order.</summary>
    private static PersonDto[] EnqueueSingleRiders(FakeRideQueueService queue, int count) =>
        [.. Enumerable.Range(0, count).Select(_ => queue.EnqueueSingleRider(MakeSingleRider()))];

    /// <summary>
    /// Loads the ride so that exactly five seats are free: two empty gondolas plus the
    /// seat beside a group's lone member.
    /// </summary>
    private static void LeaveFiveFreeSeats(RideStore store)
    {
        LeaveEmptyGondolas(store, 3);
        store.BoardGroup([new PassengerWeight(75d)]);
        Assert.Equal(5, store.FreeSeatCount);
        Assert.Equal(2, store.EmptyGondolaCount);
    }

    private static int PassengersAboard(RideStore store) => store.GetTelemetry().BoardedPassengerCount;

    [Fact]
    public async Task RunLoadingPass_BoardsAFittingGroup_BeforeAnySingleRider()
    {
        var (coordinator, store, queue, rideId) = Loading();
        var group = queue.Enqueue(MakeGroup(3));
        EnqueueSingleRiders(queue, 2);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        // Both phases ran in the one pass, and the group was taken first.
        Assert.Equal([$"group:{group.GroupId}", "singles:2"], queue.Takes);
        Assert.Equal(5, PassengersAboard(store));
        Assert.Empty(queue.Groups);
        Assert.Empty(queue.SingleRiders);
    }

    [Fact]
    public async Task RunLoadingPass_AGroupTakingTheLastGondola_LeavesNoSeatForSingleRiders()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 1);
        queue.Enqueue(MakeGroup(2));
        var singles = EnqueueSingleRiders(queue, 3);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        // The one empty gondola went to the group, not to two of the waiting singles.
        Assert.Empty(queue.Groups);
        Assert.Equal(singles, queue.SingleRiders);
        Assert.Equal(0, store.FreeSeatCount);
    }

    [Fact]
    public async Task RunLoadingPass_KeepsTheLookAheadBackfill_AndTopsUpOnlyTheSeatTheGroupLeft()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 2);
        var big = queue.Enqueue(MakeGroup(6));   // needs 3 gondolas — does not fit
        var mid = queue.Enqueue(MakeGroup(3));   // needs 2 — fits, stranding one seat
        var tail = queue.Enqueue(MakeGroup(9));
        var singles = EnqueueSingleRiders(queue, 3);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        // The second group still backfills exactly as before...
        Assert.DoesNotContain(queue.Groups, g => g.GroupId == mid.GroupId);
        Assert.Equal(new[] { big.GroupId, tail.GroupId }, queue.Groups.Select(g => g.GroupId));
        Assert.Equal(6, big.Size);

        // ...and only then does the first single rider take the seat beside its odd member.
        Assert.Equal(0, store.FreeSeatCount);
        Assert.Equal(singles[1..], queue.SingleRiders);
        Assert.Equal($"group:{mid.GroupId}", queue.Takes[0]);
    }

    [Fact]
    public async Task RunLoadingPass_WhenNoWaitingGroupFits_TopsUpTheFreeSeats_WithoutSplittingAGroup()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 2);
        queue.Enqueue(MakeGroup(6));
        queue.Enqueue(MakeGroup(6));
        queue.Enqueue(MakeGroup(6));
        var singles = EnqueueSingleRiders(queue, 5);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        // No group fits two gondolas, so none is taken or split...
        Assert.Equal([6, 6, 6], queue.Groups.Select(g => g.Size));

        // ...and the four free seats go to the four longest-waiting single riders.
        Assert.Equal(0, store.FreeSeatCount);
        Assert.Equal(singles[4..], queue.SingleRiders);
    }

    [Fact]
    public async Task RunLoadingPass_WithAnEmptyGroupQueue_SeatsSingleRiders_TwoToAGondola()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 1);
        EnqueueSingleRiders(queue, 2);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Empty(queue.SingleRiders);
        Assert.Equal(0, store.EmptyGondolaCount);
        Assert.Equal(0, store.FreeSeatCount);
    }

    [Fact]
    public async Task RunLoadingPass_FillsEveryFreeSeat_WhenEnoughSingleRidersWait()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveFiveFreeSeats(store);
        var singles = EnqueueSingleRiders(queue, 6);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Equal(0, store.FreeSeatCount);
        Assert.Equal(TotalSeats, PassengersAboard(store));
        // The five who boarded left the line; the sixth is now at its front.
        Assert.Equal(singles[5..], queue.SingleRiders);
    }

    [Fact]
    public async Task RunLoadingPass_WithFewerSingleRidersThanSeats_SeatsThemAll_AndLeavesSeatsFree()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveFiveFreeSeats(store);
        EnqueueSingleRiders(queue, 2);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Empty(queue.SingleRiders);
        Assert.Equal(3, store.FreeSeatCount);
    }

    [Fact]
    public async Task RunLoadingPass_SeatsSingleRidersInArrivalOrder()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 1); // two free seats
        var singles = EnqueueSingleRiders(queue, 3); // A, B, C

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        // A and B boarded; C is now at the front of the line.
        Assert.Equal(singles[2..], queue.SingleRiders);
        var aboard = store.GetTelemetry().Gondolas.SelectMany(g => g.Seats).Sum(s => s.OccupiedKg);
        var expected = ((TotalSeats - 2) * 75d) + singles[0].WeightInKilograms + singles[1].WeightInKilograms;
        Assert.Equal(expected, aboard);
    }

    [Fact]
    public async Task RunLoadingPass_OnAFullRide_TakesNoSingleRider()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 0);
        var singles = EnqueueSingleRiders(queue, 2);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Empty(queue.Takes);
        Assert.Equal(singles, queue.SingleRiders);
        Assert.Equal(TotalSeats, PassengersAboard(store));
    }

    [Fact]
    public async Task RunLoadingPass_WithAnEmptySingleRidersLine_SeatsNobody_AndLeavesTheSeatsFree()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveFiveFreeSeats(store);

        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Equal(5, store.FreeSeatCount);
        Assert.Equal(RideState.Loading, store.CurrentState);
        Assert.Empty(queue.Requeues);
    }

    [Fact]
    public async Task RunLoadingPass_AGroupIsStillRefusedTheSeatBesideASingleRider()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveEmptyGondolas(store, 1);
        EnqueueSingleRiders(queue, 1);
        await coordinator.RunLoadingPassAsync(rideId, Ct); // the single takes one seat of the last gondola
        Assert.Equal(1, store.FreeSeatCount);

        var pair = queue.Enqueue(MakeGroup(2));
        var loner = queue.Enqueue(MakeGroup(1));
        await coordinator.RunLoadingPassAsync(rideId, Ct);

        // One seat is free, but a group — even a party of one — needs a whole empty gondola.
        Assert.Equal(new[] { pair.GroupId, loner.GroupId }, queue.Groups.Select(g => g.GroupId));
        Assert.Equal(1, store.FreeSeatCount);
    }

    // Single riders — state gating.

    [Theory]
    [InlineData(RideState.Idle)]
    [InlineData(RideState.Safe)]
    [InlineData(RideState.Started)]
    public async Task RunLoadingPass_TakesAndSeatsNoSingleRider_WhenTheRideIsNotLoading(RideState state)
    {
        var store = NewStore();
        if (state != RideState.Idle)
        {
            // An empty ride is safe, so it can be driven through Safe to Started.
            store.RequestStateTransition(RideState.Loading);
            store.RequestStateTransition(RideState.Safe);
        }

        if (state == RideState.Started)
        {
            store.RequestStateTransition(RideState.Started);
        }

        var queue = new FakeRideQueueService();
        var singles = EnqueueSingleRiders(queue, 3);
        var coordinator = NewCoordinator(store, queue);

        await coordinator.RunLoadingPassAsync(Guid.NewGuid(), Ct);

        Assert.Equal(state, store.CurrentState);
        Assert.Empty(queue.Takes);
        Assert.Equal(singles, queue.SingleRiders);
        Assert.Equal(0, PassengersAboard(store));
    }

    [Fact]
    public async Task RunLoadingPass_WhenCancelled_TakesNoSingleRider()
    {
        var (coordinator, store, queue, rideId) = Loading();
        var singles = EnqueueSingleRiders(queue, 2);

        await coordinator.RunLoadingPassAsync(rideId, new CancellationToken(canceled: true));

        Assert.Empty(queue.Takes);
        Assert.Equal(singles, queue.SingleRiders);
        Assert.Equal(0, PassengersAboard(store));
    }

    // Single riders — mid-loading arrivals and the hand-back.

    [Fact]
    public async Task RunLoadingPass_SeatsASingleRiderWhoJoinsDuringLoading_OnALaterPass()
    {
        var (coordinator, store, queue, rideId) = Loading();
        LeaveFiveFreeSeats(store);

        // First pass: nobody is waiting, so the seats stay free.
        await coordinator.RunLoadingPassAsync(rideId, Ct);
        Assert.Equal(5, store.FreeSeatCount);

        // An individual joins mid-loading; the next pass seats them.
        EnqueueSingleRiders(queue, 1);
        await coordinator.RunLoadingPassAsync(rideId, Ct);

        Assert.Empty(queue.SingleRiders);
        Assert.Equal(4, store.FreeSeatCount);
    }

    [Fact]
    public async Task RunLoadingPass_WhenSeatingFails_ReturnsTheRidersToTheFront_InTheirOriginalOrder()
    {
        var store = new Mock<IRideStore>();
        store.SetupGet(s => s.CurrentState).Returns(RideState.Loading);
        store.SetupGet(s => s.EmptyGondolaCount).Returns(0);
        store.SetupGet(s => s.FreeSeatCount).Returns(2);
        store
            .Setup(s => s.SeatSingleRiders(It.IsAny<IReadOnlyList<PassengerWeight>>()))
            .Throws(new DomainValidationException("no seat after all"));
        var queue = new FakeRideQueueService();
        var singles = EnqueueSingleRiders(queue, 3); // A, B, C
        var coordinator = new RideLoadingCoordinator(store.Object, NullLogger<RideLoadingCoordinator>.Instance, queue);

        // The failure still surfaces, so the pass's span and the simulation loop's log record it...
        await Assert.ThrowsAsync<DomainValidationException>(
            () => coordinator.RunLoadingPassAsync(Guid.NewGuid(), Ct));

        // ...but A and B, who were taken while C kept waiting, are back ahead of C.
        Assert.Equal(["singles:2"], queue.Takes);
        Assert.Equal(singles, queue.SingleRiders);
        Assert.Equal(singles[..2], Assert.Single(queue.Requeues));
    }

    [Fact]
    public async Task RunLoadingPass_WhenSeatsDisappearAfterTheTake_SeatsWhoFits_AndReturnsTheRestToTheFront()
    {
        var store = new Mock<IRideStore>();
        store.SetupGet(s => s.CurrentState).Returns(RideState.Loading);
        store.SetupGet(s => s.EmptyGondolaCount).Returns(0);
        // Three seats are free when the riders are taken, one when they are seated,
        // and none once the top-up is logged.
        store.SetupSequence(s => s.FreeSeatCount).Returns(3).Returns(1).Returns(0);
        IReadOnlyList<PassengerWeight>? seated = null;
        store
            .Setup(s => s.SeatSingleRiders(It.IsAny<IReadOnlyList<PassengerWeight>>()))
            .Callback<IReadOnlyList<PassengerWeight>>(riders => seated = riders)
            .Returns(new RideStore(new RandomRideEventSampler(seed: 1)).GetTelemetry());
        var queue = new FakeRideQueueService();
        var singles = EnqueueSingleRiders(queue, 4); // A, B, C, D
        var coordinator = new RideLoadingCoordinator(store.Object, NullLogger<RideLoadingCoordinator>.Instance, queue);

        await coordinator.RunLoadingPassAsync(Guid.NewGuid(), Ct);

        // Only A is seated; B and C go back in front of D, who was never taken.
        Assert.NotNull(seated);
        Assert.Equal(singles[0].WeightInKilograms, Assert.Single(seated).Kilograms);
        Assert.Equal(singles[1..], queue.SingleRiders);
    }

    [Fact]
    public async Task RunLoadingPass_WhenTheRideLeavesLoadingAfterTheTake_SeatsNobody_AndReturnsEveryone()
    {
        var store = new Mock<IRideStore>();
        // Loading at the start of the pass and of the top-up, Safe by the time of seating.
        store.SetupSequence(s => s.CurrentState)
            .Returns(RideState.Loading)
            .Returns(RideState.Loading)
            .Returns(RideState.Safe);
        store.SetupGet(s => s.EmptyGondolaCount).Returns(0);
        store.SetupGet(s => s.FreeSeatCount).Returns(2);
        var queue = new FakeRideQueueService();
        var singles = EnqueueSingleRiders(queue, 3);
        var coordinator = new RideLoadingCoordinator(store.Object, NullLogger<RideLoadingCoordinator>.Instance, queue);

        await coordinator.RunLoadingPassAsync(Guid.NewGuid(), Ct);

        store.Verify(s => s.SeatSingleRiders(It.IsAny<IReadOnlyList<PassengerWeight>>()), Times.Never);
        Assert.Equal(singles, queue.SingleRiders);
    }

    /// <summary>
    /// In-memory <see cref="IRideQueueService"/> for the coordinator tests: an ordered
    /// list of groups and an ordered single-riders line, supporting the status
    /// snapshot, take-by-id, bulk take and front-of-line requeue the coordinator uses.
    /// The Enqueue helpers only exist to arrange the lines; the real service is covered
    /// by the Queue module's own tests.
    /// </summary>
    private sealed class FakeRideQueueService : IRideQueueService
    {
        private readonly List<QueuedGroupDto> _groups = [];
        private readonly List<PersonDto> _singleRiders = [];
        private readonly List<string> _takes = [];
        private readonly List<IReadOnlyList<PersonDto>> _requeues = [];

        public IReadOnlyList<QueuedGroupDto> Groups => _groups;

        /// <summary>The single-riders line, front first.</summary>
        public IReadOnlyList<PersonDto> SingleRiders => _singleRiders;

        /// <summary>Every successful take, in order: <c>group:&lt;id&gt;</c> or <c>singles:&lt;count taken&gt;</c>.</summary>
        public IReadOnlyList<string> Takes => _takes;

        /// <summary>Every batch of riders handed back to the front of the line.</summary>
        public IReadOnlyList<IReadOnlyList<PersonDto>> Requeues => _requeues;

        public QueuedGroupDto Enqueue(QueuedGroupDto group)
        {
            _groups.Add(group);
            return group;
        }

        public PersonDto EnqueueSingleRider(PersonDto person)
        {
            _singleRiders.Add(person);
            return person;
        }

        public Task<IReadOnlyList<QueuedGroupDto>> EnqueueGroupAsync(Guid rideId, int groupSize, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public GetQueueStatusResponse GetStatus(Guid rideId) =>
            new(rideId, _groups.Count, _groups.Sum(g => g.Size), [.. _groups], _singleRiders.Count, [.. _singleRiders]);

        public Task<QueuedGroupDto?> TakeGroupAsync(Guid rideId, Guid groupId, CancellationToken cancellationToken)
        {
            var index = _groups.FindIndex(g => g.GroupId == groupId);
            if (index < 0)
            {
                return Task.FromResult<QueuedGroupDto?>(null);
            }

            var group = _groups[index];
            _groups.RemoveAt(index);
            _takes.Add($"group:{group.GroupId}");
            return Task.FromResult<QueuedGroupDto?>(group);
        }

        public Task<PersonDto> EnqueueSingleRiderAsync(Guid rideId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<PersonDto>> TakeSingleRidersAsync(Guid rideId, int count, CancellationToken cancellationToken)
        {
            var take = Math.Clamp(count, 0, _singleRiders.Count);
            IReadOnlyList<PersonDto> taken = _singleRiders.GetRange(0, take);
            _singleRiders.RemoveRange(0, take);
            if (take > 0)
            {
                _takes.Add($"singles:{take}");
            }

            return Task.FromResult(taken);
        }

        public Task RequeueSingleRidersAsync(Guid rideId, IReadOnlyList<PersonDto> people, CancellationToken cancellationToken)
        {
            _singleRiders.InsertRange(0, people);
            _requeues.Add([.. people]);
            return Task.CompletedTask;
        }
    }
}
