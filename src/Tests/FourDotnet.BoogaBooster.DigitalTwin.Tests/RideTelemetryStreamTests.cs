using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>The SSE telemetry broadcast producer: cadence, running-gate, and cancellation.</summary>
public sealed class RideTelemetryStreamTests
{
    [Fact]
    public async Task It_emits_a_frame_per_telemetry_interval_while_running()
    {
        var store = RunningStore();
        var clock = new FakeTimeProvider();
        var stream = new RideTelemetryStream(store, clock);

        await using var frames = stream.Stream(CancellationToken.None).GetAsyncEnumerator();

        // The first frame is emitted immediately (before the first delay).
        Assert.True(await frames.MoveNextAsync());

        // Each subsequent frame arrives one telemetry interval later.
        var second = frames.MoveNextAsync();
        Assert.False(second.IsCompleted);
        clock.Advance(RideParameters.TelemetryInterval);
        Assert.True(await second);

        var third = frames.MoveNextAsync();
        Assert.False(third.IsCompleted);
        clock.Advance(RideParameters.TelemetryInterval);
        Assert.True(await third);
    }

    [Fact]
    public async Task It_emits_nothing_while_the_ride_is_not_running_then_resumes_once_it_runs()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1)); // idle
        var clock = new FakeTimeProvider();
        var stream = new RideTelemetryStream(store, clock);

        await using var frames = stream.Stream(CancellationToken.None).GetAsyncEnumerator();

        // Idle: the loop keeps ticking but never yields a frame.
        var pending = frames.MoveNextAsync();
        Assert.False(pending.IsCompleted);
        clock.Advance(RideParameters.TelemetryInterval * 10);
        Assert.False(pending.IsCompleted);

        // Once the ride starts, the very next tick yields a frame.
        DriveToStarted(store);
        clock.Advance(RideParameters.TelemetryInterval);
        Assert.True(await pending);
        Assert.Equal(RideState.Started, frames.Current.State);
    }

    [Fact]
    public async Task It_stops_promptly_when_the_client_disconnects()
    {
        var store = RunningStore();
        var clock = new FakeTimeProvider();
        var stream = new RideTelemetryStream(store, clock);
        using var cts = new CancellationTokenSource();

        await using var frames = stream.Stream(cts.Token).GetAsyncEnumerator();
        Assert.True(await frames.MoveNextAsync()); // first frame

        var next = frames.MoveNextAsync(); // awaiting the next interval
        Assert.False(next.IsCompleted);

        await cts.CancelAsync();
        Assert.False(await next); // the stream ends gracefully
    }

    [Fact]
    public async Task Each_frame_is_a_full_ride_snapshot()
    {
        var store = RunningStore();
        var clock = new FakeTimeProvider();
        var stream = new RideTelemetryStream(store, clock);

        await using var frames = stream.Stream(CancellationToken.None).GetAsyncEnumerator();
        Assert.True(await frames.MoveNextAsync());

        var frame = frames.Current;
        Assert.Equal(RideParameters.HubCount, frame.Hubs.Count);
        Assert.Equal(RideParameters.HubCount * RideParameters.GondolasPerHub, frame.Gondolas.Count);
        Assert.All(frame.Gondolas, g => Assert.Equal(RideParameters.SeatsPerGondola, g.Seats.Count));
        Assert.NotNull(frame.Mill);
    }

    private static RideStore RunningStore()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        DriveToStarted(store);
        return store;
    }

    private static void DriveToStarted(RideStore store)
    {
        // The empty ride is safe, so it can walk straight up to Started.
        store.RequestStateTransition(RideState.Loading);
        store.RequestStateTransition(RideState.Safe);
        store.RequestStateTransition(RideState.Started);
    }
}
