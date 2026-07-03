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
    public void Sampled_restraint_delays_stay_within_10_to_30_seconds()
    {
        var sampler = new RandomRideEventSampler(seed: 7);

        for (var i = 0; i < 1000; i++)
        {
            var delay = sampler.NextRestraintCloseDelay();
            Assert.InRange(delay, RideParameters.MinRestraintCloseDelay, RideParameters.MaxRestraintCloseDelay);
        }
    }

    [Fact]
    public void The_same_seed_produces_the_same_sequence()
    {
        var a = new RandomRideEventSampler(seed: 42);
        var b = new RandomRideEventSampler(seed: 42);

        Assert.Equal(a.NextPassengerWeight().Kilograms, b.NextPassengerWeight().Kilograms);
        Assert.Equal(a.NextRestraintCloseDelay(), b.NextRestraintCloseDelay());
    }
}
