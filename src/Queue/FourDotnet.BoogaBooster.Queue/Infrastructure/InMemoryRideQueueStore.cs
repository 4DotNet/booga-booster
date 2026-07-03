using System.Collections.Concurrent;
using FourDotnet.BoogaBooster.Queue.Domain;
using Microsoft.Extensions.Options;

namespace FourDotnet.BoogaBooster.Queue.Infrastructure;

/// <summary>
/// Thread-safe, process-lifetime <see cref="IRideQueueStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Registered as a singleton so
/// the background filler and the API share the same queues.
/// </summary>
internal sealed class InMemoryRideQueueStore : IRideQueueStore
{
    private readonly ConcurrentDictionary<Guid, RideQueue> _queues = new();
    private readonly int _maxQueueLength;

    public InMemoryRideQueueStore(IOptions<QueueModuleOptions> options)
    {
        _maxQueueLength = options.Value.MaxQueueLength;
    }

    public RideQueue GetOrCreate(Guid rideId) =>
        _queues.GetOrAdd(rideId, id => new RideQueue(id, _maxQueueLength));

    public RideQueue? Find(Guid rideId) =>
        _queues.TryGetValue(rideId, out var queue) ? queue : null;
}
