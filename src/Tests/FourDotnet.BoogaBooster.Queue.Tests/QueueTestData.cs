using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Filling;
using Microsoft.Extensions.Options;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Shared factories for the Queue tests: builds simple people, profiles and group
/// arrivals without pulling in randomness, a fixed clock reading and the default
/// grumpiness policy for tests that drive the wait explicitly, and a seeded
/// <see cref="PersonGenerator"/> for tests that exercise real generation.
/// </summary>
internal static class QueueTestData
{
    /// <summary>A fixed instant to stamp groups with when a test does not care about time.</summary>
    public static readonly DateTimeOffset Now = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);

    /// <summary>The grumpiness policy the module's default options produce (5 min onset, 0.01/min).</summary>
    public static readonly GrumpinessPolicy DefaultPolicy = PolicyFrom(new QueueModuleOptions());

    /// <summary>A valid, neutral rider profile; override any value as needed.</summary>
    public static RiderProfile Profile(double preferredIntensity = 0.5, double happiness = 0.75, double nausea = 0.0) =>
        new(preferredIntensity, happiness, nausea);

    /// <summary>A valid person with sensible defaults; override any field as needed.</summary>
    public static Person Person(
        long number = 1,
        string name = "Test Person",
        int weightInKilograms = 80,
        RiderProfile? profile = null) =>
        new(number, name, weightInKilograms, profile ?? Profile());

    /// <summary>A group arrival of <paramref name="size"/> distinct, valid people sharing the neutral profile.</summary>
    public static GroupArrival Group(int size) =>
        new(Guid.NewGuid(), Enumerable.Range(1, size).Select(n => Person(number: n)).ToArray());

    /// <summary>A group arrival of one person per given arrival happiness, numbered in order.</summary>
    public static GroupArrival GroupWithHappiness(params double[] happiness) =>
        new(
            Guid.NewGuid(),
            happiness.Select((h, i) => Person(number: i + 1, profile: Profile(happiness: h))).ToArray());

    /// <summary>A queue with the default grumpiness policy; override the limits or policy as needed.</summary>
    public static RideQueue Queue(int maxPeople = 100, int maxBoardableGroupSize = 32, GrumpinessPolicy? policy = null) =>
        new(Guid.NewGuid(), maxPeople, maxBoardableGroupSize, policy ?? DefaultPolicy);

    /// <summary>The grumpiness policy <paramref name="options"/> describe, as the store would build it.</summary>
    public static GrumpinessPolicy PolicyFrom(QueueModuleOptions options) =>
        new(options.GrumpinessOnset, options.GrumpinessRatePerMinute);

    /// <summary>A deterministically seeded person generator.</summary>
    public static PersonGenerator Generator(int? seed = 123) =>
        new(Options.Create(new QueueModuleOptions { RandomSeed = seed }));
}
