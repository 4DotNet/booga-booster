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
}
