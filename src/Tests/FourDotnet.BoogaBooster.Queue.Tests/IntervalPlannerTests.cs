using FourDotnet.BoogaBooster.Queue;
using FourDotnet.BoogaBooster.Queue.Filling;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the pure interval-planning logic that paces the ride queue filler: which
/// weather band a draw comes from, and that the draw stays inside that band.
/// </summary>
public sealed class IntervalPlannerTests
{
    private static QueueModuleOptions Defaults() => new();

    [Theory]
    [InlineData(1.0)]
    [InlineData(0.9)]
    [InlineData(0.5)]
    [InlineData(0.41)]
    public void PlanFillInterval_InFairWeather_DrawsFromTheFairWeatherBand(double niceWeather)
    {
        var options = Defaults();
        var rng = new Random(42);

        for (var i = 0; i < 200; i++)
        {
            Assert.InRange(
                IntervalPlanner.PlanFillInterval(niceWeather, options, rng),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30));
        }
    }

    [Theory]
    [InlineData(0.4)]
    [InlineData(0.2)]
    [InlineData(0.0)]
    public void PlanFillInterval_InBadWeather_DrawsFromTheBadWeatherBand(double niceWeather)
    {
        var options = Defaults();
        var rng = new Random(42);

        for (var i = 0; i < 200; i++)
        {
            Assert.InRange(
                IntervalPlanner.PlanFillInterval(niceWeather, options, rng),
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(60));
        }
    }

    [Fact]
    public void PlanFillInterval_SpansItsBand_AcrossManyDraws()
    {
        var options = Defaults();
        var rng = new Random(7);

        var draws = Enumerable.Range(0, 500)
            .Select(_ => IntervalPlanner.PlanFillInterval(1.0, options, rng))
            .ToList();

        // Not a fixed cadence, and the draw genuinely explores both ends of [10, 30].
        Assert.True(draws.Distinct().Count() > 1);
        Assert.True(draws.Min() < TimeSpan.FromSeconds(15));
        Assert.True(draws.Max() > TimeSpan.FromSeconds(25));
    }

    [Fact]
    public void PlanFillInterval_ClampsOutOfRangeNiceWeather()
    {
        var options = Defaults();
        var rng = new Random(1);

        // Above 1 clamps to fair weather, below 0 clamps to bad weather.
        Assert.InRange(
            IntervalPlanner.PlanFillInterval(5.0, options, rng),
            TimeSpan.FromSeconds(10),
            TimeSpan.FromSeconds(30));
        Assert.InRange(
            IntervalPlanner.PlanFillInterval(-5.0, options, rng),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void PlanFillInterval_HonoursACustomThreshold()
    {
        var options = Defaults();
        options.BadWeatherThreshold = 0.8;
        var rng = new Random(3);

        // 0.7 is fair under the default threshold but bad under this one.
        Assert.InRange(
            IntervalPlanner.PlanFillInterval(0.7, options, rng),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void PlanFillInterval_WithEqualBounds_ReturnsThatExactInterval()
    {
        var options = Defaults();
        options.MinFillInterval = TimeSpan.FromSeconds(20);
        options.MaxFillInterval = TimeSpan.FromSeconds(20);

        Assert.Equal(TimeSpan.FromSeconds(20), IntervalPlanner.PlanFillInterval(1.0, options, new Random(1)));
    }

    [Fact]
    public void PlanFillInterval_RejectsInvalidBounds()
    {
        var inverted = Defaults();
        inverted.MinFillInterval = TimeSpan.FromSeconds(30);
        inverted.MaxFillInterval = TimeSpan.FromSeconds(10);

        var nonPositive = Defaults();
        nonPositive.MinFillInterval = TimeSpan.Zero;

        Assert.Throws<ArgumentOutOfRangeException>(
            () => IntervalPlanner.PlanFillInterval(1.0, inverted, new Random(1)));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IntervalPlanner.PlanFillInterval(1.0, nonPositive, new Random(1)));
    }

    [Fact]
    public void PlanFillInterval_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => IntervalPlanner.PlanFillInterval(1.0, null!, new Random(1)));
        Assert.Throws<ArgumentNullException>(
            () => IntervalPlanner.PlanFillInterval(1.0, Defaults(), null!));
    }
}
