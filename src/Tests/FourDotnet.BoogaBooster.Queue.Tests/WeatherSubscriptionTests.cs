using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using FourDotnet.BoogaBooster.Queue.Endpoints;
using FourDotnet.BoogaBooster.Queue.Filling;
using Microsoft.Extensions.Options;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the weather-update subscriber handler and the shared
/// <see cref="WeatherInfluence"/> state it writes: newest-wins, clamping,
/// the neutral pre-event default and the topic contract.
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
    public void Handler_UpdatesState_WithTheEventNiceWeather()
    {
        var influence = NewInfluence();

        var result = WeatherSubscriptionEndpoints.HandleWeatherUpdate(EventWith(0.4f), influence);

        Assert.NotNull(result);
        Assert.Equal(0.4, influence.Current, precision: 5);
    }

    [Fact]
    public void Handler_ClampsOutOfRangeValues_IntoUnitInterval()
    {
        var influence = NewInfluence();

        WeatherSubscriptionEndpoints.HandleWeatherUpdate(EventWith(1.7f), influence);
        Assert.Equal(1.0, influence.Current, precision: 5);

        WeatherSubscriptionEndpoints.HandleWeatherUpdate(EventWith(-0.5f), influence);
        Assert.Equal(0.0, influence.Current, precision: 5);
    }

    [Fact]
    public void Handler_NewestEventWins_RegardlessOfOrder()
    {
        var influence = NewInfluence();

        WeatherSubscriptionEndpoints.HandleWeatherUpdate(EventWith(0.8f), influence);
        WeatherSubscriptionEndpoints.HandleWeatherUpdate(EventWith(0.2f), influence);

        Assert.Equal(0.2, influence.Current, precision: 5);
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
