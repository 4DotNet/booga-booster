using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Weather.Abstractions;

namespace FourDotnet.BoogaBooster.Weather.Features.GetWeather;

/// <summary>Reads the current weather conditions.</summary>
public sealed record GetWeatherQuery : Query<WeatherConditionDto>;
