using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Default <see cref="IRideEventSampler"/>. Draws passenger weights, rider profiles
/// and restraint delays from uniform distributions. An optional seed makes every run
/// reproducible — the discipline the whole twin depends on for testable physics.
/// </summary>
public sealed class RandomRideEventSampler : IRideEventSampler
{
    private readonly Random _random;

    public RandomRideEventSampler()
        : this(null)
    {
    }

    public RandomRideEventSampler(int? seed)
    {
        _random = seed is { } value ? new Random(value) : new Random();
    }

    public PassengerWeight NextPassengerWeight()
    {
        var kilograms = RideParameters.MinPassengerKg
            + (_random.NextDouble() * (RideParameters.MaxPassengerKg - RideParameters.MinPassengerKg));
        return new PassengerWeight(kilograms);
    }

    public RiderProfile NextRiderProfile()
    {
        var preferredIntensity = RiderProfile.MinPreferredIntensity
            + (_random.NextDouble() * (RiderProfile.MaxPreferredIntensity - RiderProfile.MinPreferredIntensity));
        var happiness = RideParameters.MinBoardingHappiness
            + (_random.NextDouble() * (RideParameters.MaxBoardingHappiness - RideParameters.MinBoardingHappiness));
        return new RiderProfile(preferredIntensity, happiness, RiderProfile.MinMood);
    }

    public TimeSpan NextRestraintCloseDelay()
    {
        var min = RideParameters.MinRestraintCloseDelay.TotalSeconds;
        var max = RideParameters.MaxRestraintCloseDelay.TotalSeconds;
        var seconds = min + (_random.NextDouble() * (max - min));
        return TimeSpan.FromSeconds(seconds);
    }

    public IReadOnlyList<int> NextGondolaSelection(int total, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, total);

        // Partial Fisher–Yates: shuffle only the first `count` positions out of `total`,
        // which yields `count` distinct, uniformly-chosen indices without allocating a
        // full shuffle beyond what we need.
        var indices = new int[total];
        for (var i = 0; i < total; i++)
        {
            indices[i] = i;
        }

        for (var i = 0; i < count; i++)
        {
            var j = i + _random.Next(total - i);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }

        var selection = new int[count];
        Array.Copy(indices, selection, count);
        return selection;
    }
}
