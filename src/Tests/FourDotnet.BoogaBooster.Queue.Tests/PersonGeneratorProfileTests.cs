using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Filling;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the rider profile <see cref="PersonGenerator"/> draws for every arriving
/// guest: preference across the whole permitted range, happiness in the cheerful
/// arrival band, no nausea, and the same sequence for the same seed.
/// </summary>
public sealed class PersonGeneratorProfileTests
{
    [Fact]
    public void Next_OverAThousandGuests_DrawsProfilesWithinTheArrivalRanges()
    {
        var generator = QueueTestData.Generator(seed: 2026);

        var profiles = Enumerable.Range(0, 1000).Select(_ => generator.Next().Profile).ToArray();

        Assert.All(
            profiles,
            profile => Assert.InRange(
                profile.PreferredIntensity,
                RiderProfile.MinPreferredIntensity,
                RiderProfile.MaxPreferredIntensity));
        Assert.All(
            profiles,
            profile => Assert.InRange(
                profile.Happiness,
                PersonGenerator.MinArrivalHappiness,
                PersonGenerator.MaxArrivalHappiness));
        Assert.All(profiles, profile => Assert.Equal(0, profile.Nausea));
    }

    [Fact]
    public void Next_OverAThousandGuests_SpreadsThePreferenceAcrossTheRange()
    {
        // A uniform draw over [0.1, 1] should visit both the tame and the intense
        // half; a generator stuck on one value or one half would pass the range
        // check above and still be wrong.
        var generator = QueueTestData.Generator(seed: 2026);

        var preferences = Enumerable.Range(0, 1000).Select(_ => generator.Next().Profile.PreferredIntensity).ToArray();

        Assert.Contains(preferences, p => p < 0.4);
        Assert.Contains(preferences, p => p > 0.7);
        Assert.True(preferences.Distinct().Count() > 900);
    }

    [Fact]
    public void Next_WithTheSameSeed_ReproducesTheProfilesInOrder()
    {
        var first = QueueTestData.Generator(seed: 42);
        var second = QueueTestData.Generator(seed: 42);

        var firstProfiles = Enumerable.Range(0, 10).Select(_ => first.Next().Profile).ToArray();
        var secondProfiles = Enumerable.Range(0, 10).Select(_ => second.Next().Profile).ToArray();

        Assert.Equal(firstProfiles, secondProfiles);
    }

    [Fact]
    public void Next_WithDifferentSeeds_DrawsDifferentProfiles()
    {
        var first = QueueTestData.Generator(seed: 1);
        var second = QueueTestData.Generator(seed: 2);

        var firstProfiles = Enumerable.Range(0, 10).Select(_ => first.Next().Profile).ToArray();
        var secondProfiles = Enumerable.Range(0, 10).Select(_ => second.Next().Profile).ToArray();

        Assert.NotEqual(firstProfiles, secondProfiles);
    }

    [Fact]
    public void CreateGroup_GivesEveryMemberTheirOwnProfile()
    {
        var arrival = QueueTestData.Generator(seed: 7).CreateGroup(5);

        Assert.All(arrival.Members, member => Assert.NotNull(member.Profile));
        Assert.True(arrival.Members.Select(m => m.Profile).Distinct().Count() > 1);
    }
}
