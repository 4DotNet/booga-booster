using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Default <see cref="IRideEventSampler"/>. Draws passenger weights and restraint
/// delays from a uniform distribution. An optional seed makes every run
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

    public TimeSpan NextRestraintCloseDelay()
    {
        var min = RideParameters.MinRestraintCloseDelay.TotalSeconds;
        var max = RideParameters.MaxRestraintCloseDelay.TotalSeconds;
        var seconds = min + (_random.NextDouble() * (max - min));
        return TimeSpan.FromSeconds(seconds);
    }
}
