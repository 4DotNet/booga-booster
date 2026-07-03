using FourDotnet.BoogaBooster.IntegrationMessages.Events.Queue;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using Xunit;

namespace FourDotnet.BoogaBooster.IntegrationMessages.Tests;

/// <summary>
/// Covers the integration-event contracts: their topic declarations, the
/// <see cref="TopicNameAttribute"/> guard, and the event record payloads.
/// </summary>
public class IntegrationEventContractTests
{
    [Fact]
    public void TopicNameAttribute_RejectsBlankName()
    {
        Assert.Throws<ArgumentException>(() => new TopicNameAttribute(" "));
    }

    [Fact]
    public void TopicNameAttribute_KeepsTheName()
    {
        var attribute = new TopicNameAttribute("weather-updated");

        Assert.Equal("weather-updated", attribute.Name);
    }

    [Fact]
    public void WeatherUpdateEvent_DeclaresTopic_AndCarriesPayload()
    {
        var @event = new WeatherUpdateIntegrationEvent(
            TemperatureCelsius: 12.5,
            WindBeaufort: 4,
            SunshinePercent: 40,
            Precipitation: "Rain",
            Regime: "Precipitation",
            NiceWeather: 0.25f);

        Assert.Equal("weather-updated", TopicNameResolver.Resolve<WeatherUpdateIntegrationEvent>());
        Assert.Equal(12.5, @event.TemperatureCelsius);
        Assert.Equal(4, @event.WindBeaufort);
        Assert.Equal(40, @event.SunshinePercent);
        Assert.Equal("Rain", @event.Precipitation);
        Assert.Equal("Precipitation", @event.Regime);
        Assert.Equal(0.25f, @event.NiceWeather);
        Assert.IsAssignableFrom<IIntegrationEvent>(@event);
    }

    [Fact]
    public void WeatherUpdateEvent_HasValueEquality()
    {
        var a = new WeatherUpdateIntegrationEvent(20, 2, 70, "None", "Calm", 1f);
        var b = new WeatherUpdateIntegrationEvent(20, 2, 70, "None", "Calm", 1f);

        Assert.Equal(a, b);
    }

    [Fact]
    public void GroupQueuedEvent_DeclaresTopic_AndCarriesPayload()
    {
        var rideId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var queuedAt = DateTimeOffset.UnixEpoch;

        var @event = new GroupQueuedIntegrationEvent(rideId, groupId, PeopleCount: 3, queuedAt);

        Assert.Equal("group-queued", TopicNameResolver.Resolve<GroupQueuedIntegrationEvent>());
        Assert.Equal(rideId, @event.RideId);
        Assert.Equal(groupId, @event.GroupId);
        Assert.Equal(3, @event.PeopleCount);
        Assert.Equal(queuedAt, @event.QueuedAt);
        Assert.IsAssignableFrom<IIntegrationEvent>(@event);
    }
}
