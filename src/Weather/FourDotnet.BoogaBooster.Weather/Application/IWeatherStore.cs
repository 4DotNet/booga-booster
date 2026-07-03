using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Application;

/// <summary>
/// The result of a mutation against the store: whether the conditions actually
/// changed, and the resulting snapshot.
/// </summary>
public sealed record WeatherMutationResult(bool Changed, WeatherConditionDto Snapshot);

/// <summary>
/// Holds the single in-memory world <c>Weather</c> aggregate behind a thread-safe
/// boundary shared by the simulation loop (writer), the read query (reader), and
/// the event commands (mutators). Reads always return a consistent snapshot.
/// </summary>
public interface IWeatherStore
{
    /// <summary>Returns a consistent snapshot of the current conditions.</summary>
    WeatherConditionDto GetSnapshot();

    /// <summary>Advances the simulation by one <paramref name="elapsed"/> step.</summary>
    WeatherMutationResult Advance(TimeSpan elapsed);

    /// <summary>Begins a precipitation event of the given type.</summary>
    WeatherMutationResult StartPrecipitation(PrecipitationType type);

    /// <summary>Begins a strong-wind event.</summary>
    WeatherMutationResult StartStrongWind();
}
