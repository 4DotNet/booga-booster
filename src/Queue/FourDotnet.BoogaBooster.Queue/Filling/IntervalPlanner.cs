namespace FourDotnet.BoogaBooster.Queue.Filling;

/// <summary>
/// Pure planning logic for the wait before the next fill cycle. The wait is drawn
/// uniformly from one of two configured bands — a fair-weather band and a longer
/// bad-weather band — picked by the current <c>NiceWeather</c> indicator. Kept free
/// of time, DI and randomness ownership (the caller injects a seeded
/// <see cref="Random"/>) so it is deterministically testable.
/// </summary>
public static class IntervalPlanner
{
    /// <summary>
    /// Picks the wait before the next fill cycle. When
    /// <paramref name="niceWeather"/> sits at or below
    /// <see cref="QueueModuleOptions.BadWeatherThreshold"/> the wait is drawn from
    /// the bad-weather band, otherwise from the fair-weather band; either way the
    /// draw is uniform across the band's inclusive bounds.
    /// </summary>
    public static TimeSpan PlanFillInterval(double niceWeather, QueueModuleOptions options, Random rng)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(rng);

        var nice = Math.Clamp(niceWeather, 0.0, 1.0);
        var badWeather = nice <= options.BadWeatherThreshold;

        var min = badWeather ? options.BadWeatherMinFillInterval : options.MinFillInterval;
        var max = badWeather ? options.BadWeatherMaxFillInterval : options.MaxFillInterval;

        if (min <= TimeSpan.Zero || max < min)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Fill-interval bounds must be positive with max >= min; got [{min}, {max}].");
        }

        return min == max
            ? min
            : TimeSpan.FromTicks(rng.NextInt64(min.Ticks, max.Ticks + 1));
    }
}
