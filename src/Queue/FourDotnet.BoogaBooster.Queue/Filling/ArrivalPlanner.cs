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
    /// Scales a base headcount by the weather multiplier derived from
    /// <paramref name="niceWeather"/>: <c>Ceiling * NiceWeather^Exponent</c>,
    /// floored to a non-negative integer. The mapping is monotonic in
    /// <paramref name="niceWeather"/>, reaches the ceiling at <c>1</c>, and floors
    /// to exactly <c>0</c> at <c>0</c> so the worst weather stops arrivals
    /// entirely. Only the total headcount is scaled; group partitioning and the
    /// queue cap are unaffected.
    /// </summary>
    public static int ScaleForWeather(int baseCount, double niceWeather, QueueModuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (baseCount <= 0)
        {
            return 0;
        }

        var nice = Math.Clamp(niceWeather, 0.0, 1.0);

        // Worst weather stops arrivals outright, independent of the exponent.
        if (nice <= 0.0)
        {
            return 0;
        }

        var multiplier = options.WeatherMultiplierCeiling
            * Math.Pow(nice, options.WeatherSuppressionExponent);

        var scaled = (int)Math.Floor(baseCount * multiplier);
        return scaled < 0 ? 0 : scaled;
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
