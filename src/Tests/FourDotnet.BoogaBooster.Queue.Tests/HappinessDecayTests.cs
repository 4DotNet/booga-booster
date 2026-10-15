using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers queue happiness decay (design D2): no change within the grace period,
/// then exponential decay that halves every half-life — with the module defaults
/// of a five-minute grace period and a ten-minute half-life.
/// </summary>
public sealed class HappinessDecayTests
{
    private const double Precision = 1e-9;

    private static HappinessDecay DefaultDecay()
    {
        var options = new QueueModuleOptions();
        return new HappinessDecay(options.HappinessGracePeriod, options.HappinessHalfLife);
    }

    [Fact]
    public void Options_DefaultToAFiveMinuteGraceAndATenMinuteHalfLife()
    {
        var options = new QueueModuleOptions();

        Assert.Equal(TimeSpan.FromMinutes(5), options.HappinessGracePeriod);
        Assert.Equal(TimeSpan.FromMinutes(10), options.HappinessHalfLife);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(5)]
    public void Apply_WithinTheGracePeriod_KeepsTheStartingHappiness(double minutes)
    {
        Assert.Equal(80d, DefaultDecay().Apply(80d, TimeSpan.FromMinutes(minutes)));
    }

    [Theory]
    [InlineData(6, 74.64263932294459)]  // 80 · 2^(−0.1)
    [InlineData(10, 56.568542494923804)] // 80 · 2^(−0.5)
    [InlineData(15, 40)]                 // one half-life past the grace period
    [InlineData(25, 20)]                 // two half-lives
    public void Apply_AfterTheGracePeriod_DecaysExponentially(double minutes, double expected)
    {
        Assert.Equal(expected, DefaultDecay().Apply(80d, TimeSpan.FromMinutes(minutes)), Precision);
    }

    [Fact]
    public void Apply_AfterTheGracePeriod_KeepsFallingWhileTheGuestWaits()
    {
        var decay = DefaultDecay();

        var readings = new[] { 6d, 10d, 15d, 25d }
            .Select(minutes => decay.Apply(80d, TimeSpan.FromMinutes(minutes)))
            .ToArray();

        for (var i = 1; i < readings.Length; i++)
        {
            Assert.True(readings[i] < readings[i - 1], $"Reading {i} ({readings[i]}) did not fall below {readings[i - 1]}.");
        }
    }

    [Fact]
    public void Apply_HonoursAConfiguredGraceAndHalfLife()
    {
        var decay = new HappinessDecay(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(2));

        Assert.Equal(25d, decay.Apply(100d, TimeSpan.FromMinutes(5)), Precision);
    }

    [Fact]
    public void Constructor_WithNegativeGracePeriod_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new HappinessDecay(TimeSpan.FromSeconds(-1), TimeSpan.FromMinutes(10)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveHalfLife_Throws(int minutes)
    {
        Assert.Throws<DomainValidationException>(() => new HappinessDecay(TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(minutes)));
    }

    [Fact]
    public void Person_HappinessAfterWaiting_DecaysTheArrivalHappinessWithoutChangingIt()
    {
        var person = QueueTestData.Person(happiness: 80);

        var current = person.HappinessAfterWaiting(TimeSpan.FromMinutes(15), DefaultDecay());

        Assert.Equal(40d, current, Precision);
        Assert.Equal(80d, person.Happiness);
    }

    [Fact]
    public void Person_HappinessAfterWaiting_NullDecay_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => QueueTestData.Person().HappinessAfterWaiting(TimeSpan.Zero, null!));
    }
}
