namespace FourDotnet.BoogaBooster.Weather.Domain;

/// <summary>
/// The source of randomness the simulation uses for its per-tick noise. Injected
/// so the evolution stays a pure, reproducible function of its inputs: a seeded
/// sampler makes a run deterministic and unit-testable.
/// </summary>
public interface IWeatherSampler
{
    /// <summary>Returns a random sample in the closed interval [-1, 1].</summary>
    double Sample();
}
