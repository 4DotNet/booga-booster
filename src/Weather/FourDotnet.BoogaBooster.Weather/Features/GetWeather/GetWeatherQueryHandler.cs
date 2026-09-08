using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Observability;

namespace FourDotnet.BoogaBooster.Weather.Features.GetWeather;

/// <summary>Returns the current conditions from the weather store.</summary>
public sealed class GetWeatherQueryHandler : QueryHandler<GetWeatherQuery, WeatherConditionDto>
{
    private readonly IWeatherStore _store;

    public GetWeatherQueryHandler(IWeatherStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    protected override Task<WeatherConditionDto> ExecuteAsync(GetWeatherQuery query, CancellationToken cancellationToken)
        => Task.FromResult(_store.GetSnapshot());

    /// <summary>
    /// The query carries no payload, so the span describes what came back: the regime
    /// in force and the nice-weather reading the Queue module scales its arrivals by.
    /// </summary>
    protected override void EnrichActivityWithResponse(Activity activity, WeatherConditionDto response)
    {
        activity.SetTag(WeatherTelemetryAttributes.Regime, response.Regime.ToString());
        activity.SetTag(WeatherTelemetryAttributes.NiceWeather, response.NiceWeather);
    }
}
