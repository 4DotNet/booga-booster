using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Features.GetWeather;
using FourDotnet.BoogaBooster.Weather.Tests.Testing;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

/// <summary>
/// Covers the thin application services that expose and read the world weather.
/// </summary>
public sealed class WeatherApplicationTests
{
    [Fact]
    public void ConditionProvider_ReturnsTheStoreSnapshot()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        var provider = new WeatherConditionProvider(store);

        var conditions = provider.GetCurrentConditions();

        Assert.Equal(store.GetSnapshot(), conditions);
    }

    [Fact]
    public void ConditionProvider_NullStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WeatherConditionProvider(null!));
    }

    [Fact]
    public async Task GetWeatherQueryHandler_ReturnsCurrentConditions()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        var handler = new GetWeatherQueryHandler(store);

        var result = await handler.HandleAsync(new GetWeatherQuery(), CancellationToken.None);

        Assert.Equal(store.GetSnapshot(), result);
    }

    [Fact]
    public void GetWeatherQueryHandler_NullStore_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GetWeatherQueryHandler(null!));
    }

    [Fact]
    public async Task NoOpPublisher_DoesNothing_AndCompletes()
    {
        var publisher = new NoOpWeatherUpdatePublisher();
        var snapshot = new WeatherConditionDto(20, 2, 70, PrecipitationType.None, WeatherRegime.Calm, NiceWeather: 1f);

        // Simply completing without touching any transport is the whole contract.
        await publisher.PublishAsync(snapshot, CancellationToken.None);
    }

    [Fact]
    public void IntegrationPublisher_NullInner_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new IntegrationWeatherUpdatePublisher(null!));
    }

    [Fact]
    public async Task IntegrationPublisher_NullSnapshot_Throws()
    {
        var inner = new Mock<IntegrationMessages.IIntegrationEventPublisher>();
        var publisher = new IntegrationWeatherUpdatePublisher(inner.Object);

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => publisher.PublishAsync(null!, CancellationToken.None));
    }
}
