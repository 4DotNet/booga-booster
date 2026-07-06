using System.Runtime.CompilerServices;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Produces the ride's telemetry broadcast: a stream of full <see cref="RideTelemetry"/>
/// snapshots sampled from the store at the fixed telemetry rate
/// (<see cref="RideParameters.TelemetryInterval"/>), decoupled from the physics
/// timestep. Frames are emitted only while the ride is running; while it is at rest
/// the loop keeps ticking (holding the connection open) but yields nothing. Timing
/// runs off an injected <see cref="TimeProvider"/> so the loop is deterministic under
/// a fake clock in tests. Each connection samples independently; there is no shared
/// broadcaster state.
/// </summary>
public sealed class RideTelemetryStream
{
    private readonly IRideStore _store;
    private readonly TimeProvider _timeProvider;

    public RideTelemetryStream(IRideStore store, TimeProvider timeProvider)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Streams telemetry frames until the client disconnects (the token is cancelled).
    /// Emits one snapshot per <see cref="RideParameters.TelemetryInterval"/> while the
    /// ride is running; stays quiet (but alive) otherwise.
    /// </summary>
    public async IAsyncEnumerable<RideTelemetry> Stream(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_store.IsRunning)
            {
                yield return _store.GetTelemetry();
            }

            try
            {
                await Task.Delay(RideParameters.TelemetryInterval, _timeProvider, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }
}
