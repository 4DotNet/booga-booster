namespace FourDotnet.BoogaBooster.Queue.Filling;

/// <summary>
/// Pure planning logic for a fill cycle: how many people arrive and how they
/// partition into groups. Kept free of time, DI and randomness ownership (the
/// caller injects a seeded <see cref="Random"/>) so it is deterministically
/// testable.
/// </summary>
public static class ArrivalPlanner
{
    /// <summary>
    /// Picks a total headcount for one cycle, uniformly in
    /// <c>[minArrivals, maxArrivals]</c>.
    /// </summary>
    public static int PlanArrivalCount(int minArrivals, int maxArrivals, Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        if (minArrivals < 0 || maxArrivals < minArrivals)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxArrivals),
                "Arrival bounds must be non-negative with max >= min.");
        }

        return rng.Next(minArrivals, maxArrivals + 1);
    }

    /// <summary>
    /// Splits <paramref name="totalPeople"/> into a sequence of group sizes, each
    /// within <c>[minGroupSize, maxGroupSize]</c> and together summing exactly to
    /// the total. Returns an empty list when the total is zero.
    /// </summary>
    public static IReadOnlyList<int> PlanGroupSizes(
        int totalPeople,
        int minGroupSize,
        int maxGroupSize,
        Random rng)
    {
        ArgumentNullException.ThrowIfNull(rng);

        if (minGroupSize < 1 || maxGroupSize < minGroupSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxGroupSize),
                "Group-size bounds must be positive with max >= min.");
        }

        if (totalPeople < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPeople), "Total people cannot be negative.");
        }

        var groups = new List<int>();
        var remaining = totalPeople;

        while (remaining > 0)
        {
            // What is left fits in one group — emit it and stop.
            if (remaining <= maxGroupSize)
            {
                groups.Add(remaining);
                break;
            }

            // Cap the take so the leftover is still large enough to form a valid
            // group; every emitted size then stays within [min, max].
            var maxTake = Math.Min(maxGroupSize, remaining - minGroupSize);
            if (maxTake < minGroupSize)
            {
                // Bounds cannot partition the remainder in two — keep it whole.
                groups.Add(remaining);
                break;
            }

            var size = rng.Next(minGroupSize, maxTake + 1);
            groups.Add(size);
            remaining -= size;
        }

        return groups;
    }
}
