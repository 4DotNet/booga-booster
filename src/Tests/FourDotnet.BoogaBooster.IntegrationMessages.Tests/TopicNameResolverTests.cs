using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using Xunit;

namespace FourDotnet.BoogaBooster.IntegrationMessages.Tests;

public class TopicNameResolverTests
{
    [TopicName("cached-topic")]
    private sealed record DecoratedEvent : IIntegrationEvent;

    private sealed record UndecoratedEvent : IIntegrationEvent;

    [Fact]
    public void Resolve_ReturnsTopicName_FromAttribute()
    {
        var topic = TopicNameResolver.Resolve<WeatherChangedIntegrationEvent>();

        Assert.Equal("weather-changed", topic);
    }

    [Fact]
    public void Resolve_ReturnsSameCachedInstance_ForRepeatedCalls()
    {
        var first = TopicNameResolver.Resolve(typeof(DecoratedEvent));
        var second = TopicNameResolver.Resolve(typeof(DecoratedEvent));

        Assert.Equal("cached-topic", first);
        // The resolved string is cached per type, so repeated lookups return the very
        // same interned instance rather than recomputing via reflection.
        Assert.Same(first, second);
    }

    [Fact]
    public void Resolve_Throws_WhenTopicNameAttributeMissing()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => TopicNameResolver.Resolve(typeof(UndecoratedEvent)));

        Assert.Contains(nameof(UndecoratedEvent), exception.Message);
        Assert.Contains("[TopicName", exception.Message);
    }
}
