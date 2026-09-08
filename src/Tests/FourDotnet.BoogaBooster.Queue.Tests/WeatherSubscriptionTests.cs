using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using FourDotnet.BoogaBooster.Queue.Endpoints;
using FourDotnet.BoogaBooster.Queue.Features.RecordWeatherUpdate;
using FourDotnet.BoogaBooster.Queue.Filling;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the weather-update subscription endpoint — that it maps the integration
/// event onto a <see cref="RecordWeatherUpdateCommand"/> and dispatches it rather
/// than touching state itself (ADR-0005) — plus the topic contract and the
/// pre-event default of the <see cref="WeatherInfluence"/> state it drives.
/// </summary>
public sealed class WeatherSubscriptionTests
{
    private static WeatherInfluence NewInfluence(double neutralDefault = 1.0) =>
        new(Options.Create(new QueueModuleOptions { NeutralWeatherDefault = neutralDefault }));

    private static WeatherUpdateIntegrationEvent EventWith(float niceWeather) =>
        new(
            TemperatureCelsius: 20,
            WindBeaufort: 2,
            SunshinePercent: 50,
            Precipitation: "None",
            Regime: "Calm",
            NiceWeather: niceWeather);

    [Fact]
    public async Task Endpoint_DispatchesCommand_CarryingTheEventNiceWeather()
    {
        var handler = new Mock<ICommandHandler<RecordWeatherUpdateCommand>>();

        var result = await WeatherSubscriptionEndpoints.HandleWeatherUpdate(
            EventWith(0.4f),
            handler.Object,
            TestContext.Current.CancellationToken);

        Assert.NotNull(result);
        handler.Verify(
            h => h.HandleAsync(
                It.Is<RecordWeatherUpdateCommand>(c => Math.Abs(c.NiceWeather - 0.4f) < 1e-6),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Endpoint_PassesItsCancellationToken_ToTheHandler()
    {
        var handler = new Mock<ICommandHandler<RecordWeatherUpdateCommand>>();
        using var cts = new CancellationTokenSource();

        await WeatherSubscriptionEndpoints.HandleWeatherUpdate(EventWith(0.6f), handler.Object, cts.Token);

        handler.Verify(
            h => h.HandleAsync(It.IsAny<RecordWeatherUpdateCommand>(), cts.Token),
            Times.Once);
    }

    [Fact]
    public async Task Endpoint_NullEvent_Throws()
    {
        var handler = new Mock<ICommandHandler<RecordWeatherUpdateCommand>>();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            WeatherSubscriptionEndpoints.HandleWeatherUpdate(
                null!,
                handler.Object,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Endpoint_NullHandler_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            WeatherSubscriptionEndpoints.HandleWeatherUpdate(
                EventWith(0.5f),
                null!,
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Influence_ReportsNeutralDefault_BeforeAnyEvent()
    {
        var influence = NewInfluence(neutralDefault: 1.0);

        Assert.Equal(1.0, influence.Current, precision: 5);
    }

    [Fact]
    public void Influence_ClampsNeutralDefault_OnConstruction()
    {
        var influence = NewInfluence(neutralDefault: 3.0);

        Assert.Equal(1.0, influence.Current, precision: 5);
    }

    [Fact]
    public void Subscriber_TopicName_MatchesEventContract()
    {
        // The subscriber resolves its topic from the same attribute the publisher
        // uses, so published messages reach the endpoint.
        Assert.Equal("weather-updated", TopicNameResolver.Resolve<WeatherUpdateIntegrationEvent>());
    }
}
