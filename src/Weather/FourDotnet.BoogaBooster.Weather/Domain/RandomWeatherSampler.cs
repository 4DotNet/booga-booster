namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// Default <see cref="IWeatherSampler"/> backed by <see cref="Random"/>. Accepts
/// an optional seed so tests can make the evolution reproducible.
/// </summary>
public sealed class RandomWeatherSampler : IWeatherSampler
{
    private readonly Random _random;

    /// <summary>Creates a sampler with a non-deterministic seed.</summary>
    public RandomWeatherSampler()
        : this(Random.Shared.Next())
    {
    }

    /// <summary>Creates a sampler seeded with <paramref name="seed"/>.</summary>
    public RandomWeatherSampler(int seed)
    {
        _random = new Random(seed);
    }

    /// <inheritdoc />
    public double Sample() => (_random.NextDouble() * 2d) - 1d;
}
