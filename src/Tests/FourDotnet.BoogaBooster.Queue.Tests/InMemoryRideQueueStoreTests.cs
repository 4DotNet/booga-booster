using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the in-memory store's two per-ride lines: each is created on first access,
/// sized from its own option, found again afterwards, and independent of the other.
/// </summary>
public sealed class InMemoryRideQueueStoreTests
{
    private static InMemoryRideQueueStore NewStore(QueueModuleOptions? options = null) =>
        new(Options.Create(options ?? new QueueModuleOptions()));

    [Fact]
    public void Options_DefaultTheSinglesLine_To200People_AndOneToThreeArrivals()
    {
        var options = new QueueModuleOptions();

        Assert.Equal(200, options.MaxSingleRiderQueueLength);
        Assert.Equal(1, options.MinSingleArrivalsPerCycle);
        Assert.Equal(3, options.MaxSingleArrivalsPerCycle);
    }

    [Fact]
    public void FindSingles_ForARideNobodyJoined_ReturnsNull()
    {
        Assert.Null(NewStore().FindSingles(Guid.NewGuid()));
    }

    [Fact]
    public void GetOrCreateSingles_CreatesTheLineOnce_SizedFromItsOwnOption()
    {
        var rideId = Guid.NewGuid();
        var store = NewStore(new QueueModuleOptions { MaxQueueLength = 500, MaxSingleRiderQueueLength = 7 });

        var singles = store.GetOrCreateSingles(rideId);

        Assert.Equal(rideId, singles.RideId);
        Assert.Equal(7, singles.MaxPeople);
        Assert.Same(singles, store.GetOrCreateSingles(rideId));
        Assert.Same(singles, store.FindSingles(rideId));
    }

    [Fact]
    public void GetOrCreateSingles_KeepsALinePerRide()
    {
        var store = NewStore();

        var first = store.GetOrCreateSingles(Guid.NewGuid());
        var second = store.GetOrCreateSingles(Guid.NewGuid());

        Assert.NotSame(first, second);
    }

    [Fact]
    public void TheTwoLines_AreCreatedIndependently()
    {
        var rideId = Guid.NewGuid();
        var store = NewStore();

        store.GetOrCreateSingles(rideId);
        Assert.Null(store.Find(rideId));

        var otherRide = Guid.NewGuid();
        store.GetOrCreate(otherRide);
        Assert.Null(store.FindSingles(otherRide));
    }
}
