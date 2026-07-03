using Dapr.Client;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.IntegrationMessages.Tests;

public class DaprIntegrationEventPublisherTests
{
    private sealed record MissingTopicEvent : IIntegrationEvent;

    [Fact]
    public async Task PublishAsync_PublishesToResolvedTopic_OnConfiguredPubSub()
    {
        var daprClient = new Mock<DaprClient>();
        daprClient
            .Setup(c => c.PublishEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<WeatherChangedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var publisher = new DaprIntegrationEventPublisher(daprClient.Object);
        var integrationEvent = new WeatherChangedIntegrationEvent(
            "Amsterdam", 21, "Warm", DateTimeOffset.UtcNow);

        await publisher.PublishAsync(integrationEvent, CancellationToken.None);

        daprClient.Verify(c => c.PublishEventAsync(
            IntegrationMessagingDefaults.PubSubName,
            "weather-changed",
            integrationEvent,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PublishAsync_Throws_WhenEventHasNoTopicName()
    {
        var daprClient = new Mock<DaprClient>();
        var publisher = new DaprIntegrationEventPublisher(daprClient.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => publisher.PublishAsync(new MissingTopicEvent(), CancellationToken.None));

        daprClient.Verify(c => c.PublishEventAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<MissingTopicEvent>(),
            It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
