using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Domain;
using FourDotnet.BoogaBooster.Weather.Features.GetWeather;
using FourDotnet.BoogaBooster.Weather.Features.StartPrecipitation;
using FourDotnet.BoogaBooster.Weather.Features.StartStrongWind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FourDotnet.BoogaBooster.Weather;

/// <summary>
/// Composition entry point for the Weather module (ADR-0007). A host wires the
/// module up with <see cref="AddWeatherModule"/> and
/// <see cref="Endpoints.WeatherEndpoints.MapWeatherEndpoints"/>.
/// </summary>
public static class WeatherModuleExtensions
{
    /// <summary>
    /// Registers the Weather module's services: the in-memory store, the
    /// randomness sampler, the weather-update publisher, the cross-module
    /// condition provider, the feature handlers, and the hosted simulation loop.
    /// The integration-message transport itself is registered once at the host
    /// level via <c>AddBoogaBoosterIntegrationMessages()</c>.
    /// </summary>
    public static IHostApplicationBuilder AddWeatherModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        services.TryAddSingleton(TimeProvider.System);

        // Simulation state and evolution.
        services.TryAddSingleton<IWeatherSampler, RandomWeatherSampler>();
        services.TryAddSingleton<IWeatherStore, WeatherStore>();
        services.TryAddSingleton<IWeatherConditionProvider, WeatherConditionProvider>();

        // Publishes weather updates through the central integration-messages publisher.
        services.TryAddSingleton<IWeatherUpdatePublisher, IntegrationWeatherUpdatePublisher>();

        // Feature handlers (CQRS — ADR-0005/0006).
        services.AddScoped<IQueryHandler<GetWeatherQuery, WeatherConditionDto>, GetWeatherQueryHandler>();
        services.AddScoped<ICommandHandler<StartPrecipitationCommand>, StartPrecipitationCommandHandler>();
        services.AddScoped<ICommandHandler<StartStrongWindCommand>, StartStrongWindCommandHandler>();

        // The autonomous simulation loop.
        services.AddHostedService<WeatherSimulationService>();

        return builder;
    }
}
