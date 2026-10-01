using FourDotnet.BoogaBooster.Queue.Filling;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the initial experience ratings <see cref="PersonGenerator"/> draws on
/// arrival (docs/06-passenger-experience.md §6.1): happiness in [65, 85], preferred
/// intensity in [50, 100], nausea exactly 0 — and reproducible from the seed.
/// </summary>
public sealed class PersonGeneratorExperienceTests
{
    [Fact]
    public void Generated_guests_start_within_their_initial_ranges()
    {
        var generator = QueueTestData.Generator(seed: 2026);

        for (var i = 0; i < 1_000; i++)
        {
            var person = generator.Next();

            Assert.InRange(person.Happiness, PersonGenerator.MinArrivalHappiness, PersonGenerator.MaxArrivalHappiness);
            Assert.InRange(
                person.PreferredIntensity,
                PersonGenerator.MinArrivalPreferredIntensity,
                PersonGenerator.MaxArrivalPreferredIntensity);
            Assert.Equal(0d, person.Nausea);
        }
    }

    [Fact]
    public void Generated_ratings_are_spread_across_their_ranges_not_constant()
    {
        var generator = QueueTestData.Generator(seed: 2026);
        var people = Enumerable.Range(0, 1_000).Select(_ => generator.Next()).ToArray();

        // A uniform draw over 1,000 guests lands in both halves of each range.
        Assert.Contains(people, p => p.Happiness < 75d);
        Assert.Contains(people, p => p.Happiness > 75d);
        Assert.Contains(people, p => p.PreferredIntensity < 75d);
        Assert.Contains(people, p => p.PreferredIntensity > 75d);
    }

    [Fact]
    public void The_same_seed_reproduces_the_same_ratings()
    {
        var first = QueueTestData.Generator(seed: 42);
        var second = QueueTestData.Generator(seed: 42);

        for (var i = 0; i < 10; i++)
        {
            var a = first.Next();
            var b = second.Next();

            Assert.Equal(a.Happiness, b.Happiness);
            Assert.Equal(a.PreferredIntensity, b.PreferredIntensity);
            Assert.Equal(a.Nausea, b.Nausea);
        }
    }
}
