using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Domain;
using WeatherModel = FourDotnet.BoogaBooster.Weather.Domain.Weather;

namespace FourDotnet.BoogaBooster.Weather.Application;

/// <summary>
/// Default <see cref="IWeatherStore"/>. Guards the single <see cref="Weather"/>
/// aggregate with a lock so the simulation loop, the read query, and the event
/// commands never see or produce a half-updated state. The weather is ephemeral:
/// it starts at the moderate defaults and is not persisted.
/// </summary>
public sealed class WeatherStore : IWeatherStore
{
    private readonly Lock _gate = new();
    private readonly IWeatherSampler _sampler;
    private readonly WeatherModel _weather;

    public WeatherStore(IWeatherSampler sampler)
    {
        _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
        _weather = WeatherModel.CreateDefault();
    }

    public WeatherConditionDto GetSnapshot()
    {
        lock (_gate)
        {
            return _weather.ToConditionDto();
        }
    }

    public WeatherMutationResult Advance(TimeSpan elapsed)
    {
        lock (_gate)
        {
            var changed = _weather.Advance(elapsed, _sampler);
            return new WeatherMutationResult(changed, _weather.ToConditionDto());
        }
    }

    public WeatherMutationResult StartPrecipitation(PrecipitationType type)
    {
        lock (_gate)
        {
            _weather.StartPrecipitation(type);
            return new WeatherMutationResult(Changed: true, _weather.ToConditionDto());
        }
    }

    public WeatherMutationResult StartStrongWind()
    {
        lock (_gate)
        {
            _weather.StartStrongWind();
            return new WeatherMutationResult(Changed: true, _weather.ToConditionDto());
        }
    }
}
