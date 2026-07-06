using Microsoft.Extensions.Options;

namespace FourDotnet.BoogaBooster.Queue.Filling;

/// <summary>
/// In-memory singleton <see cref="IWeatherInfluence"/>. Holds the latest
/// <c>NiceWeather</c> indicator as a single <see cref="double"/> guarded for
/// atomic read/write (<see cref="Volatile"/>/<see cref="Interlocked"/>), so the
/// per-request subscriber and the singleton filler can hand the value across
/// without tearing. Starts at the configured neutral default.
/// </summary>
internal sealed class WeatherInfluence : IWeatherInfluence
{
    private double _current;

    public WeatherInfluence(IOptions<QueueModuleOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _current = Math.Clamp(options.Value.NeutralWeatherDefault, 0.0, 1.0);
    }

    public double Current => Volatile.Read(ref _current);

    public void Update(float niceWeather)
    {
        var clamped = Math.Clamp((double)niceWeather, 0.0, 1.0);
        Interlocked.Exchange(ref _current, clamped);
    }
}
