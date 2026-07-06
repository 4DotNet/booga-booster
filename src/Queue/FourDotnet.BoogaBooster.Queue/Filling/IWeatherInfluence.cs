namespace FourDotnet.BoogaBooster.Queue.Filling;

/// <summary>
/// Thread-safe hand-off point for the latest observed weather. The weather
/// subscriber writes the most recent <c>NiceWeather</c> indicator and the hosted
/// filler reads it each cycle to scale how many guests arrive.
/// </summary>
public interface IWeatherInfluence
{
    /// <summary>
    /// The most recently observed <c>NiceWeather</c> indicator, always within
    /// <c>[0, 1]</c>. Reports the configured neutral default until the first
    /// weather-update event is received.
    /// </summary>
    double Current { get; }

    /// <summary>
    /// Records the latest <c>NiceWeather</c> indicator, clamping it into
    /// <c>[0, 1]</c> before storing. The newest value always wins.
    /// </summary>
    void Update(float niceWeather);
}
