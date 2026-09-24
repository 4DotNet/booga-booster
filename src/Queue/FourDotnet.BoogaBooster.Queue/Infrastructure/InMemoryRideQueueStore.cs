using System.Collections.Concurrent;
using FourDotnet.BoogaBooster.Queue.Domain;
using Microsoft.Extensions.Options;

namespace FourDotnet.BoogaBooster.Queue.Infrastructure;

/// <summary>
/// Thread-safe, process-lifetime <see cref="IRideQueueStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Registered as a singleton so
/// the background filler and the API share the same queues. The
/// <see cref="GrumpinessPolicy"/> every queue is created with is built once here
/// from <see cref="QueueModuleOptions"/>.
/// </summary>
internal sealed class InMemoryRideQueueStore : IRideQueueStore
{
    private readonly ConcurrentDictionary<Guid, RideQueue> _queues = new();
    private readonly int _maxQueueLength;
    private readonly int _maxBoardableGroupSize;
    private readonly GrumpinessPolicy _grumpinessPolicy;

    public InMemoryRideQueueStore(IOptions<QueueModuleOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxQueueLength = options.Value.MaxQueueLength;
        _maxBoardableGroupSize = options.Value.MaxBoardableGroupSize;
        _grumpinessPolicy = new GrumpinessPolicy(
            options.Value.GrumpinessOnset,
            options.Value.GrumpinessRatePerMinute);
    }

    public RideQueue GetOrCreate(Guid rideId) =>
        _queues.GetOrAdd(rideId, id => new RideQueue(id, _maxQueueLength, _maxBoardableGroupSize, _grumpinessPolicy));

    public RideQueue? Find(Guid rideId) =>
        _queues.TryGetValue(rideId, out var queue) ? queue : null;
}
