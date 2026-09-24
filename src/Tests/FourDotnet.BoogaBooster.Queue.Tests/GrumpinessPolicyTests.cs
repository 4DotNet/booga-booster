using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the <see cref="GrumpinessPolicy"/> value object: the closed-form erosion of
/// happiness by a wait beyond the onset, its floor at zero, and its validation.
/// The scenarios use the module's default onset (5 min) and rate (0.01/min) so the
/// numbers match the specification's examples.
/// </summary>
public sealed class GrumpinessPolicyTests
{
    private const int Precision = 10;

    private static readonly GrumpinessPolicy Default = QueueTestData.DefaultPolicy;

    [Fact]
    public void DefaultOptions_ProduceAFiveMinuteOnset_AtOneHundredthPerMinute()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), Default.Onset);
        Assert.Equal(0.01, Default.RatePerMinute);
    }

    [Fact]
    public void CurrentHappiness_BeforeTheOnset_IsUnchanged()
    {
        Assert.Equal(0.8, Default.CurrentHappiness(0.8, TimeSpan.FromMinutes(4)));
    }

    [Fact]
    public void CurrentHappiness_WithNoWaitAtAll_IsUnchanged()
    {
        Assert.Equal(0.8, Default.CurrentHappiness(0.8, TimeSpan.Zero));
    }

    [Fact]
    public void CurrentHappiness_ExactlyAtTheOnset_IsUnchanged()
    {
        Assert.Equal(0.8, Default.CurrentHappiness(0.8, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void CurrentHappiness_PastTheOnset_DecreasesLinearly()
    {
        // Fifteen minutes waited is ten past the onset: 0.8 - 10 * 0.01 = 0.7.
        Assert.Equal(0.7, Default.CurrentHappiness(0.8, TimeSpan.FromMinutes(15)), Precision);
    }

    [Fact]
    public void CurrentHappiness_CountsFractionalMinutesPastTheOnset()
    {
        // Five and a half minutes is thirty seconds past the onset: 0.8 - 0.5 * 0.01.
        Assert.Equal(0.795, Default.CurrentHappiness(0.8, TimeSpan.FromMinutes(5.5)), Precision);
    }

    [Fact]
    public void CurrentHappiness_NeverDropsBelowZero()
    {
        // 0.65 would go negative after 65 minutes past the onset; wait far longer.
        Assert.Equal(0, Default.CurrentHappiness(0.65, TimeSpan.FromHours(3)));
    }

    [Fact]
    public void CurrentHappiness_WithAZeroRate_NeverChanges()
    {
        var policy = new GrumpinessPolicy(TimeSpan.Zero, ratePerMinute: 0);

        Assert.Equal(0.8, policy.CurrentHappiness(0.8, TimeSpan.FromHours(10)));
    }

    [Fact]
    public void CurrentHappiness_WithAZeroOnset_StartsErodingImmediately()
    {
        var policy = new GrumpinessPolicy(TimeSpan.Zero, ratePerMinute: 0.1);

        Assert.Equal(0.7, policy.CurrentHappiness(0.8, TimeSpan.FromMinutes(1)), Precision);
    }

    [Fact]
    public void Constructor_WithNegativeOnset_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new GrumpinessPolicy(TimeSpan.FromSeconds(-1), 0.01));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Constructor_WithNegativeOrNonFiniteRate_Throws(double ratePerMinute)
    {
        Assert.Throws<DomainValidationException>(() => new GrumpinessPolicy(TimeSpan.FromMinutes(5), ratePerMinute));
    }
}
