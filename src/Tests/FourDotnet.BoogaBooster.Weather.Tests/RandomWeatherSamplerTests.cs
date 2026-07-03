using FourDotnet.BoogaBooster.Weather.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

/// <summary>
/// Covers the default <see cref="RandomWeatherSampler"/> — its output range and
/// its reproducibility under a fixed seed.
/// </summary>
public sealed class RandomWeatherSamplerTests
{
    [Fact]
    public void Sample_StaysWithinMinusOneToOne()
    {
        var sampler = new RandomWeatherSampler();

        for (var i = 0; i < 1000; i++)
        {
            Assert.InRange(sampler.Sample(), -1d, 1d);
        }
    }

    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new RandomWeatherSampler(1234);
        var b = new RandomWeatherSampler(1234);

        var seqA = Enumerable.Range(0, 20).Select(_ => a.Sample()).ToArray();
        var seqB = Enumerable.Range(0, 20).Select(_ => b.Sample()).ToArray();

        Assert.Equal(seqA, seqB);
    }
}
