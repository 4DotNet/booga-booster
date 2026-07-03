using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.Weather;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Domain;
using FourDotnet.BoogaBooster.Weather.Features.GetWeather;
using FourDotnet.BoogaBooster.Weather.Features.StartPrecipitation;
using FourDotnet.BoogaBooster.Weather.Features.StartStrongWind;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

/// <summary>
/// Verifies the Weather module composition entry point (ADR-0007): a single
/// <see cref="WeatherModuleExtensions.AddWeatherModule"/> call registers the store,
/// sampler, provider, publisher, feature handlers, and the hosted simulation loop.
/// </summary>
public sealed class WeatherModuleExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        var builder = Host.CreateApplicationBuilder();
        // The integration-message publisher is a host-level dependency; provide a
        // stand-in so the integration weather publisher resolves.
        builder.Services.AddSingleton(new Mock<IIntegrationEventPublisher>().Object);
        builder.AddWeatherModule();
        return builder.Services.BuildServiceProvider();
    }

    [Fact]
    public void AddWeatherModule_RegistersSimulationServices()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IWeatherSampler>());
        Assert.NotNull(provider.GetRequiredService<IWeatherStore>());
        Assert.NotNull(provider.GetRequiredService<IWeatherConditionProvider>());
        Assert.NotNull(provider.GetRequiredService<IWeatherUpdatePublisher>());
    }

    [Fact]
    public void AddWeatherModule_RegistersFeatureHandlers()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<IQueryHandler<GetWeatherQuery, WeatherConditionDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<StartPrecipitationCommand>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<StartStrongWindCommand>>());
    }

    [Fact]
    public void AddWeatherModule_RegistersHostedSimulation()
    {
        using var provider = BuildProvider();

        Assert.Contains(provider.GetServices<IHostedService>(), s => s is WeatherSimulationService);
    }

    [Fact]
    public void AddWeatherModule_SharesASingleWeatherStore()
    {
        using var provider = BuildProvider();

        // The simulation loop, the read query, and the event commands must all see
        // the same in-memory world weather.
        Assert.Same(
            provider.GetRequiredService<IWeatherStore>(),
            provider.GetRequiredService<IWeatherStore>());
    }

    [Fact]
    public void AddWeatherModule_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IHostApplicationBuilder)null!).AddWeatherModule());
    }
}
