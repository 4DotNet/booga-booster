using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

public sealed class SamplerTests
{
    [Fact]
    public void Sampled_weights_stay_within_the_allowed_range()
    {
        var sampler = new RandomRideEventSampler(seed: 7);

        for (var i = 0; i < 1000; i++)
        {
            var weight = sampler.NextPassengerWeight();
            Assert.InRange(weight.Kilograms, RideParameters.MinPassengerKg, RideParameters.MaxPassengerKg);
        }
    }

    [Fact]
    public void Sampled_restraint_delays_stay_within_5_to_10_seconds()
    {
        var sampler = new RandomRideEventSampler(seed: 7);

        for (var i = 0; i < 1000; i++)
        {
            var delay = sampler.NextRestraintCloseDelay();
            Assert.InRange(delay, RideParameters.MinRestraintCloseDelay, RideParameters.MaxRestraintCloseDelay);
        }
    }

    [Fact]
    public void Sampled_rider_profiles_stay_within_the_arrival_ranges_with_no_nausea()
    {
        var sampler = new RandomRideEventSampler(seed: 7);

        for (var i = 0; i < 1000; i++)
        {
            var profile = sampler.NextRiderProfile();

            Assert.InRange(profile.PreferredIntensity, RiderProfile.MinPreferredIntensity, RiderProfile.MaxPreferredIntensity);
            Assert.InRange(profile.Happiness, RideParameters.MinBoardingHappiness, RideParameters.MaxBoardingHappiness);
            Assert.Equal(0d, profile.Nausea);
        }
    }

    [Fact]
    public void Sampled_rider_profiles_spread_across_the_preference_range()
    {
        var sampler = new RandomRideEventSampler(seed: 7);
        var profiles = Enumerable.Range(0, 1000).Select(_ => sampler.NextRiderProfile()).ToArray();

        // A uniform draw over [0.1, 1] lands in both halves; a constant would not.
        Assert.Contains(profiles, p => p.PreferredIntensity < 0.55);
        Assert.Contains(profiles, p => p.PreferredIntensity > 0.55);
    }

    [Fact]
    public void The_same_seed_produces_the_same_sequence()
    {
        var a = new RandomRideEventSampler(seed: 42);
        var b = new RandomRideEventSampler(seed: 42);

        Assert.Equal(a.NextPassengerWeight().Kilograms, b.NextPassengerWeight().Kilograms);
        Assert.Equal(a.NextRiderProfile(), b.NextRiderProfile());
        Assert.Equal(a.NextRestraintCloseDelay(), b.NextRestraintCloseDelay());
    }

    [Fact]
    public void Gondola_selection_returns_distinct_in_range_indices()
    {
        var sampler = new RandomRideEventSampler(seed: 7);

        for (var i = 0; i < 1000; i++)
        {
            var selection = sampler.NextGondolaSelection(total: 16, count: 5);

            Assert.Equal(5, selection.Count);
            Assert.Equal(5, selection.Distinct().Count());
            Assert.All(selection, index => Assert.InRange(index, 0, 15));
        }
    }

    [Fact]
    public void Selecting_every_gondola_is_a_permutation_of_all_indices()
    {
        var sampler = new RandomRideEventSampler(seed: 7);

        var selection = sampler.NextGondolaSelection(total: 16, count: 16);

        Assert.Equal(Enumerable.Range(0, 16), selection.OrderBy(index => index));
    }

    [Fact]
    public void Gondola_selection_does_not_always_follow_the_natural_order()
    {
        var sampler = new RandomRideEventSampler(seed: 7);

        var selection = sampler.NextGondolaSelection(total: 16, count: 16);

        // A random draw of all sixteen must not come back in 0..15 order — that would
        // mean load never spreads and always fills the first gondolas first.
        Assert.NotEqual(Enumerable.Range(0, 16), selection);
    }

    [Fact]
    public void Selecting_more_than_are_available_is_rejected()
    {
        var sampler = new RandomRideEventSampler(seed: 7);

        Assert.Throws<ArgumentOutOfRangeException>(() => sampler.NextGondolaSelection(total: 3, count: 4));
    }
}
