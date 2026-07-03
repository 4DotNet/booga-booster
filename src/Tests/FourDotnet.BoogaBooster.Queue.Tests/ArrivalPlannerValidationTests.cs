using FourDotnet.BoogaBooster.Queue.Filling;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the argument-validation and indivisible-remainder branches of
/// <see cref="ArrivalPlanner"/> that the happy-path tests do not reach.
/// </summary>
public class ArrivalPlannerValidationTests
{
    [Fact]
    public void PlanArrivalCount_NegativeMin_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ArrivalPlanner.PlanArrivalCount(minArrivals: -1, maxArrivals: 5, new Random(1)));
    }

    [Fact]
    public void PlanArrivalCount_MaxBelowMin_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ArrivalPlanner.PlanArrivalCount(minArrivals: 8, maxArrivals: 4, new Random(1)));
    }

    [Fact]
    public void PlanArrivalCount_NullRng_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ArrivalPlanner.PlanArrivalCount(1, 5, null!));
    }

    [Fact]
    public void PlanGroupSizes_MinBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ArrivalPlanner.PlanGroupSizes(10, minGroupSize: 0, maxGroupSize: 5, new Random(1)));
    }

    [Fact]
    public void PlanGroupSizes_MaxBelowMin_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ArrivalPlanner.PlanGroupSizes(10, minGroupSize: 5, maxGroupSize: 3, new Random(1)));
    }

    [Fact]
    public void PlanGroupSizes_NegativeTotal_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ArrivalPlanner.PlanGroupSizes(-1, minGroupSize: 1, maxGroupSize: 5, new Random(1)));
    }

    [Fact]
    public void PlanGroupSizes_NullRng_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ArrivalPlanner.PlanGroupSizes(5, 1, 5, null!));
    }

    [Theory]
    [InlineData(6)]
    [InlineData(7)]
    public void PlanGroupSizes_IndivisibleRemainder_IsKeptWhole(int total)
    {
        // With min=4/max=5 a total just above the maximum cannot be split into two
        // valid groups (the leftover would fall below the minimum), so the planner
        // keeps the remainder as a single whole group rather than producing an
        // invalid one.
        var sizes = ArrivalPlanner.PlanGroupSizes(total, minGroupSize: 4, maxGroupSize: 5, new Random(3));

        Assert.Equal(total, sizes.Sum());
        Assert.Single(sizes);
        Assert.Equal(total, sizes[0]);
    }
}
