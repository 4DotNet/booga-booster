using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Application;

namespace FourDotnet.BoogaBooster.Weather.Features.GetWeather;

/// <summary>Returns the current conditions from the weather store.</summary>
public sealed class GetWeatherQueryHandler : QueryHandler<GetWeatherQuery, WeatherConditionDto>
{
    private readonly IWeatherStore _store;

    public GetWeatherQueryHandler(IWeatherStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public override Task<WeatherConditionDto> HandleAsync(GetWeatherQuery query, CancellationToken cancellationToken)
        => Task.FromResult(_store.GetSnapshot());
}
