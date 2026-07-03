using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Domain;
using FourDotnet.BoogaBooster.Weather.Features.StartPrecipitation;
using FourDotnet.BoogaBooster.Weather.Features.StartStrongWind;
using FourDotnet.BoogaBooster.Weather.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

public sealed class WeatherPublishingTests
{
    [Fact]
    public async Task Starting_precipitation_publishes_a_weather_update()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        var publisher = new Mock<IWeatherUpdatePublisher>();
        var handler = new StartPrecipitationCommandHandler(store, publisher.Object);

        await handler.HandleAsync(new StartPrecipitationCommand(PrecipitationType.Rain), CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<WeatherConditionDto>(s => s.Regime == WeatherRegime.Precipitation && s.Precipitation == PrecipitationType.Rain),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Starting_strong_wind_publishes_a_weather_update()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        var publisher = new Mock<IWeatherUpdatePublisher>();
        var handler = new StartStrongWindCommandHandler(store, publisher.Object);

        await handler.HandleAsync(new StartStrongWindCommand(), CancellationToken.None);

        publisher.Verify(
            p => p.PublishAsync(
                It.Is<WeatherConditionDto>(s => s.Regime == WeatherRegime.StrongWind),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task A_changed_tick_publishes_and_a_no_op_tick_does_not()
    {
        var store = new Mock<IWeatherStore>();
        var snapshot = new WeatherConditionDto(19, 3, 65, PrecipitationType.None, WeatherRegime.Calm, NiceWeather: 0.9f);
        var publisher = new Mock<IWeatherUpdatePublisher>();
        var service = new WeatherSimulationService(
            store.Object, publisher.Object, TimeProvider.System, NullLogger<WeatherSimulationService>.Instance);

        store.Setup(s => s.Advance(It.IsAny<TimeSpan>()))
            .Returns(new WeatherMutationResult(Changed: true, snapshot));
        var publishedOnChange = await service.TickAsync(CancellationToken.None);

        store.Setup(s => s.Advance(It.IsAny<TimeSpan>()))
            .Returns(new WeatherMutationResult(Changed: false, snapshot));
        var publishedOnNoOp = await service.TickAsync(CancellationToken.None);

        Assert.True(publishedOnChange);
        Assert.False(publishedOnNoOp);
        publisher.Verify(p => p.PublishAsync(snapshot, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Integration_publisher_maps_the_snapshot_onto_the_integration_event()
    {
        WeatherUpdateIntegrationEvent? captured = null;
        var inner = new Mock<IIntegrationEventPublisher>();
        inner.Setup(p => p.PublishAsync(It.IsAny<WeatherUpdateIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback((WeatherUpdateIntegrationEvent e, CancellationToken _) => captured = e)
            .Returns(Task.CompletedTask);

        var publisher = new IntegrationWeatherUpdatePublisher(inner.Object);
        var snapshot = new WeatherConditionDto(12.5, 4, 40, PrecipitationType.Rain, WeatherRegime.Precipitation, NiceWeather: 0.25f);

        await publisher.PublishAsync(snapshot, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal(12.5, captured!.TemperatureCelsius);
        Assert.Equal(4, captured.WindBeaufort);
        Assert.Equal(40, captured.SunshinePercent);
        Assert.Equal(nameof(PrecipitationType.Rain), captured.Precipitation);
        Assert.Equal(nameof(WeatherRegime.Precipitation), captured.Regime);
        Assert.Equal(0.25f, captured.NiceWeather);
    }

    [Fact]
    public void Weather_update_event_declares_its_topic()
    {
        var topic = TopicNameResolver.Resolve<WeatherUpdateIntegrationEvent>();
        Assert.Equal("weather-updated", topic);
    }
}
