using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Filling;
using Microsoft.Extensions.Options;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Shared factories for the Queue tests: builds simple people and group arrivals
/// without pulling in randomness, and a seeded <see cref="PersonGenerator"/> for
/// tests that exercise real generation.
/// </summary>
internal static class QueueTestData
{
    /// <summary>A fixed, arbitrary join time for tests that do not care about waiting.</summary>
    public static readonly DateTimeOffset JoinedAt = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A valid person with sensible defaults; override any field as needed.</summary>
    public static Person Person(
        long number = 1,
        string name = "Test Person",
        int weightInKilograms = 80,
        double happiness = 75d,
        double preferredIntensity = 75d,
        double nausea = 0d) =>
        new(number, name, weightInKilograms, happiness, preferredIntensity, nausea);

    /// <summary>A group arrival made of exactly the given people.</summary>
    public static GroupArrival Group(params Person[] members) => new(Guid.NewGuid(), members);

    /// <summary>A group arrival of <paramref name="size"/> distinct, valid people.</summary>
    public static GroupArrival Group(int size) =>
        new(Guid.NewGuid(), Enumerable.Range(1, size).Select(n => Person(number: n)).ToArray());

    /// <summary>A deterministically seeded person generator.</summary>
    public static PersonGenerator Generator(int? seed = 123) =>
        new(Options.Create(new QueueModuleOptions { RandomSeed = seed }));
}
