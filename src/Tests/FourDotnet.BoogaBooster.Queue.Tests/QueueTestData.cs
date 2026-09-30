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
    /// <summary>A valid person with sensible defaults; override any field as needed.</summary>
    public static Person Person(long number = 1, string name = "Test Person", int weightInKilograms = 80) =>
        new(number, name, weightInKilograms);

    /// <summary>A group arrival of <paramref name="size"/> distinct, valid people.</summary>
    public static GroupArrival Group(int size) =>
        new(Guid.NewGuid(), Enumerable.Range(1, size).Select(n => Person(number: n)).ToArray());

    /// <summary>A deterministically seeded person generator.</summary>
    public static PersonGenerator Generator(int? seed = 123) =>
        new(Options.Create(new QueueModuleOptions { RandomSeed = seed }));
}
