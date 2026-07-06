using FourDotnet.BoogaBooster.Queue.Filling;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

public class ArrivalPlannerTests
{
    [Fact]
    public void PlanArrivalCount_StaysWithinBounds()
    {
        var rng = new Random(1234);

        for (var i = 0; i < 1000; i++)
        {
            var count = ArrivalPlanner.PlanArrivalCount(4, 8, rng);
            Assert.InRange(count, 4, 8);
        }
    }

    [Fact]
    public void PlanGroupSizes_SumsToTotal_AndRespectsBounds()
    {
        var rng = new Random(42);

        for (var total = 1; total <= 40; total++)
        {
            var sizes = ArrivalPlanner.PlanGroupSizes(total, minGroupSize: 1, maxGroupSize: 5, rng);

            Assert.Equal(total, sizes.Sum());
            Assert.All(sizes, s => Assert.InRange(s, 1, 5));
        }
    }

    [Fact]
    public void PlanGroupSizes_ZeroTotal_YieldsNoGroups()
    {
        var sizes = ArrivalPlanner.PlanGroupSizes(0, minGroupSize: 1, maxGroupSize: 5, new Random(1));
        Assert.Empty(sizes);
    }

    [Fact]
    public void PlanGroupSizes_CanProduceLoneGuestsAndGroups()
    {
        // Over many cycles the default distribution yields both singles and multi-person groups.
        var rng = new Random(7);
        var seenSingle = false;
        var seenGroup = false;

        for (var i = 0; i < 200 && (!seenSingle || !seenGroup); i++)
        {
            foreach (var size in ArrivalPlanner.PlanGroupSizes(8, minGroupSize: 1, maxGroupSize: 5, rng))
            {
                seenSingle |= size == 1;
                seenGroup |= size > 1;
            }
        }

        Assert.True(seenSingle, "Expected at least one lone guest.");
        Assert.True(seenGroup, "Expected at least one multi-person group.");
    }

    [Fact]
    public void PlanGroupSizes_HigherMinimum_NeverExceedsMaximum()
    {
        var rng = new Random(99);

        for (var total = 3; total <= 40; total++)
        {
            var sizes = ArrivalPlanner.PlanGroupSizes(total, minGroupSize: 3, maxGroupSize: 5, rng);

            Assert.Equal(total, sizes.Sum());
            // With min=3/max=5 an indivisible remainder may be kept whole; it must
            // still never fall below one person.
            Assert.All(sizes, s => Assert.True(s >= 1));
        }
    }

    [Fact]
    public void PlanArrivalCount_IsDeterministicForASeed()
    {
        var a = new Random(555);
        var b = new Random(555);

        var seqA = Enumerable.Range(0, 20).Select(_ => ArrivalPlanner.PlanArrivalCount(4, 8, a)).ToArray();
        var seqB = Enumerable.Range(0, 20).Select(_ => ArrivalPlanner.PlanArrivalCount(4, 8, b)).ToArray();

        Assert.Equal(seqA, seqB);
    }

    [Fact]
    public void ScaleForWeather_WorstWeather_FloorsToZero()
    {
        var options = new QueueModuleOptions();

        Assert.Equal(0, ArrivalPlanner.ScaleForWeather(baseCount: 8, niceWeather: 0.0, options));
    }

    [Fact]
    public void ScaleForWeather_NeutralDefaults_PreserveTheBaseRate()
    {
        // Ceiling 1.0 and exponent 1.0 at NiceWeather 1.0 leave the count unchanged.
        var options = new QueueModuleOptions();

        Assert.Equal(8, ArrivalPlanner.ScaleForWeather(baseCount: 8, niceWeather: 1.0, options));
    }

    [Fact]
    public void ScaleForWeather_IsMonotonic_InNiceWeather()
    {
        var options = new QueueModuleOptions();
        var previous = -1;

        foreach (var nice in new[] { 0.0, 0.1, 0.3, 0.5, 0.7, 0.9, 1.0 })
        {
            var scaled = ArrivalPlanner.ScaleForWeather(baseCount: 30, nice, options);
            Assert.True(scaled >= previous, $"Scaled count should not decrease as weather improves (nice={nice}).");
            previous = scaled;
        }
    }

    [Fact]
    public void ScaleForWeather_ClampsOutOfRangeNiceWeather()
    {
        var options = new QueueModuleOptions();

        Assert.Equal(0, ArrivalPlanner.ScaleForWeather(baseCount: 8, niceWeather: -2.0, options));
        Assert.Equal(8, ArrivalPlanner.ScaleForWeather(baseCount: 8, niceWeather: 2.0, options));
    }

    [Fact]
    public void ScaleForWeather_Ceiling_CanBurstAboveTheBaseRate()
    {
        var options = new QueueModuleOptions { WeatherMultiplierCeiling = 1.5 };

        // floor(10 * 1.5 * 1^1) = 15.
        Assert.Equal(15, ArrivalPlanner.ScaleForWeather(baseCount: 10, niceWeather: 1.0, options));
    }

    [Fact]
    public void ScaleForWeather_HigherExponent_SuppressesLowWeatherHarder()
    {
        var linear = new QueueModuleOptions { WeatherSuppressionExponent = 1.0 };
        var steep = new QueueModuleOptions { WeatherSuppressionExponent = 2.0 };

        var linearCount = ArrivalPlanner.ScaleForWeather(baseCount: 40, niceWeather: 0.5, linear);
        var steepCount = ArrivalPlanner.ScaleForWeather(baseCount: 40, niceWeather: 0.5, steep);

        // floor(40 * 0.5) = 20 vs floor(40 * 0.25) = 10.
        Assert.True(steepCount < linearCount, "A larger exponent should suppress mid-range weather harder.");
    }

    [Fact]
    public void ScaleForWeather_ZeroBaseCount_IsZero()
    {
        var options = new QueueModuleOptions();

        Assert.Equal(0, ArrivalPlanner.ScaleForWeather(baseCount: 0, niceWeather: 1.0, options));
    }
}
