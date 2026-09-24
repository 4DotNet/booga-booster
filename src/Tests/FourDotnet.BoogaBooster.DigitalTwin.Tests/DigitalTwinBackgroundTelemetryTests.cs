using System.Diagnostics;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers what the DigitalTwin's background work reports (ADR-0009): the 120 Hz
/// physics loop is measured with a counter and a histogram and starts <em>no</em>
/// activity per tick (design D4), while a loading pass that boards a group — or one
/// that throws — does get a span.
/// </summary>
public sealed class DigitalTwinBackgroundTelemetryTests : IDisposable
{
    private const string TickCounter = "boogabooster.ride.simulation.ticks";
    private const string TickHistogram = "boogabooster.ride.simulation.tick.duration";
    private const string BoardedCounter = "boogabooster.ride.passengers.boarded";
    private const string LoadingPassOperation = "RideLoadingPass";

    private readonly TelemetryRecorder _telemetry = new();

    public void Dispose() => _telemetry.Dispose();

    [Fact]
    public void TheSimulationLoop_CountsAndTimesEachTick()
    {
        const int Ticks = 10;

        var service = SimulationService(NewStore(), NoQueue(), Guid.NewGuid());
        var countsBefore = _telemetry.Measurements(TickCounter).Count;
        var durationsBefore = _telemetry.Measurements(TickHistogram).Count;

        for (var i = 0; i < Ticks; i++)
        {
            service.Tick();
        }

        // Other test classes tick the loop in parallel, so assert on the delta this
        // test caused rather than on a process-wide total.
        Assert.True(_telemetry.Measurements(TickCounter).Count - countsBefore >= Ticks);
        Assert.True(_telemetry.Measurements(TickHistogram).Count - durationsBefore >= Ticks);
        Assert.All(
            _telemetry.Measurements(TickCounter),
            measurement => Assert.Equal(1, measurement.Value));
        Assert.All(
            _telemetry.Measurements(TickHistogram),
            measurement => Assert.True(measurement.Value >= 0));
    }

    [Fact]
    public void TheTickInstruments_AreUntagged_SoTheSeriesStaysSingle()
    {
        var service = SimulationService(NewStore(), NoQueue(), Guid.NewGuid());

        service.Tick();

        Assert.All(_telemetry.Measurements(TickCounter), measurement => Assert.Empty(measurement.Tags));
        Assert.All(_telemetry.Measurements(TickHistogram), measurement => Assert.Empty(measurement.Tags));
    }

    [Fact]
    public void TheSimulationLoop_StartsNoActivityPerTick()
    {
        var service = SimulationService(NewStore(), NoQueue(), Guid.NewGuid());

        using (_telemetry.Scope())
        {
            for (var i = 0; i < 120; i++)
            {
                service.Tick();
            }
        }

        // A second of simulated time is 120 ticks. At a span each, a minute of ride
        // time would be 7,200 spans — which is why the tick is measured, not spanned.
        // Asserting on *every* activity in the trace, rather than on a name, is what
        // makes this fail if a span is ever added to the tick path under any name.
        Assert.Empty(_telemetry.ActivitiesInScope());
    }

    [Fact]
    public async Task ALoadingPassThatBoardsAGroup_IsTraced()
    {
        var store = NewStore();
        store.RequestStateTransition(RideState.Loading);
        var queue = new StubQueue();
        queue.Enqueue(GroupOf(4));
        var rideId = Guid.NewGuid();
        var coordinator = Coordinator(store, queue);

        using (_telemetry.Scope())
        {
            await coordinator.RunLoadingPassAsync(rideId, Ct);
        }

        var activity = _telemetry.Activity(LoadingPassOperation);
        Assert.Equal(rideId, activity.GetTagItem("ride.id"));
        Assert.Equal(1, activity.GetTagItem("ride.groups.boarded"));
        Assert.Equal(4, activity.GetTagItem("ride.passengers.boarded"));
    }

    [Fact]
    public async Task ALoadingPassThatBoardsAGroup_CountsThePassengers()
    {
        var store = NewStore();
        store.RequestStateTransition(RideState.Loading);
        var queue = new StubQueue();
        queue.Enqueue(GroupOf(6));
        var coordinator = Coordinator(store, queue);

        await coordinator.RunLoadingPassAsync(Guid.NewGuid(), Ct);

        var measurement = _telemetry.Measurement(BoardedCounter, recorded => recorded.Value == 6);
        Assert.Empty(measurement.Tags);
    }

    [Fact]
    public async Task ALoadingPassThatBoardsNobody_IsNotTraced()
    {
        var store = NewStore();
        store.RequestStateTransition(RideState.Loading);
        var coordinator = Coordinator(store, new StubQueue());

        using (_telemetry.Scope())
        {
            await coordinator.RunLoadingPassAsync(Guid.NewGuid(), Ct);
        }

        // The common case runs every tick and does nothing, so it stays span-free.
        Assert.Empty(_telemetry.Activities(LoadingPassOperation));
    }

    [Fact]
    public async Task AFailingLoadingPass_IsTraced_EvenThoughItBoardedNobody()
    {
        var store = NewStore();
        store.RequestStateTransition(RideState.Loading);
        var coordinator = Coordinator(store, new ThrowingQueue());
        var rideId = Guid.NewGuid();

        using (_telemetry.Scope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => coordinator.RunLoadingPassAsync(rideId, Ct));
        }

        // The simulation loop logs and continues, so without this span a broken pass
        // would leave no trace at all.
        var activity = _telemetry.Activity(LoadingPassOperation);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("queue unreachable", activity.StatusDescription);
        Assert.Contains(activity.Events, activityEvent => activityEvent.Name == "exception");
        Assert.Equal(rideId, activity.GetTagItem("ride.id"));
        Assert.Equal(0, activity.GetTagItem("ride.groups.boarded"));
    }

    [Fact]
    public async Task TheSimulationTick_StillSwallowsALoadingFailure_ButTheSpanRecordsIt()
    {
        var store = NewStore();
        store.RequestStateTransition(RideState.Loading);
        var rideId = Guid.NewGuid();
        var service = SimulationService(store, new ThrowingQueue(), rideId);

        using (_telemetry.Scope())
        {
            // The loop must not tear down over a boarding hiccup — but the failure has
            // to be visible somewhere, and that somewhere is the pass's own span.
            await service.TickAsync(Ct);
        }

        Assert.Equal(ActivityStatusCode.Error, _telemetry.Activity(LoadingPassOperation).Status);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static RideStore NewStore() => new(new RandomRideEventSampler(seed: 7));

    private static RideLoadingCoordinator Coordinator(RideStore store, IRideQueueService? queue)
        => new(store, NullLogger<RideLoadingCoordinator>.Instance, queue);

    private static RideSimulationService SimulationService(
        RideStore store,
        IRideQueueService? queue,
        Guid rideId)
        => new(
            store,
            Coordinator(store, queue),
            Options.Create(new DigitalTwinModuleOptions { RideId = rideId }),
            TimeProvider.System,
            NullLogger<RideSimulationService>.Instance);

    /// <summary>No Queue module wired up, so every loading pass is a no-op.</summary>
    private static IRideQueueService? NoQueue() => null;

    private static QueuedGroupDto GroupOf(int size)
        => new(
            Guid.NewGuid(),
            [.. Enumerable.Range(1, size).Select(number => new PersonDto(
                number,
                $"Person {number}",
                75,
                RideParameters.DefaultPreferredIntensity,
                RideParameters.DefaultHappiness,
                Nausea: 0d))]);

    private sealed class StubQueue : IRideQueueService
    {
        private readonly List<QueuedGroupDto> _groups = [];

        internal void Enqueue(QueuedGroupDto group) => _groups.Add(group);

        public Task<IReadOnlyList<QueuedGroupDto>> EnqueueGroupAsync(
            Guid rideId,
            int groupSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public GetQueueStatusResponse GetStatus(Guid rideId)
            => new(rideId, _groups.Count, _groups.Sum(group => group.Size), [.. _groups], AverageHappiness: null);

        public Task<QueuedGroupDto?> TakeGroupAsync(
            Guid rideId,
            Guid groupId,
            CancellationToken cancellationToken)
        {
            var index = _groups.FindIndex(group => group.GroupId == groupId);
            if (index < 0)
            {
                return Task.FromResult<QueuedGroupDto?>(null);
            }

            var group = _groups[index];
            _groups.RemoveAt(index);
            return Task.FromResult<QueuedGroupDto?>(group);
        }
    }

    private sealed class ThrowingQueue : IRideQueueService
    {
        public Task<IReadOnlyList<QueuedGroupDto>> EnqueueGroupAsync(
            Guid rideId,
            int groupSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public GetQueueStatusResponse GetStatus(Guid rideId)
            => throw new InvalidOperationException("queue unreachable");

        public Task<QueuedGroupDto?> TakeGroupAsync(
            Guid rideId,
            Guid groupId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
