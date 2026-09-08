using System.Diagnostics;
using Dapr.Client;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.IntegrationMessages.Events.Weather;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.IntegrationMessages.Tests;

/// <summary>
/// Covers the trace and metric the publisher emits per publish (ADR-0009): the
/// messaging attributes on the span, the error status and <c>error</c>-outcome
/// counter when the transport throws, and the nesting under the work that caused
/// the publish (design D7).
/// </summary>
public sealed class DaprIntegrationEventPublisherTelemetryTests : IDisposable
{
    private const string PublishOperation = "PublishIntegrationEvent";
    private const string PublishCounter = "boogabooster.messaging.events.published";

    private readonly TelemetryRecorder _telemetry = new();

    public void Dispose() => _telemetry.Dispose();

    [Fact]
    public async Task PublishAsync_TagsTheTopic_TheEventType_AndTheTransport()
    {
        var publisher = new DaprIntegrationEventPublisher(SucceedingClient().Object);

        using (_telemetry.Scope())
        {
            await publisher.PublishAsync(WeatherChanged(), TestContext.Current.CancellationToken);
        }

        var activity = _telemetry.Activity(PublishOperation);
        Assert.Equal("dapr", activity.GetTagItem("messaging.system"));
        Assert.Equal("weather-changed", activity.GetTagItem("messaging.destination.name"));
        Assert.Equal(IntegrationMessagingDefaults.PubSubName, activity.GetTagItem("messaging.dapr.component"));
        Assert.Equal(nameof(WeatherChangedIntegrationEvent), activity.GetTagItem("messaging.event.type"));
        Assert.Equal(ActivityKind.Producer, activity.Kind);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    [Fact]
    public async Task PublishAsync_CountsTheEvent_ByTopicAndOkOutcome()
    {
        var publisher = new DaprIntegrationEventPublisher(SucceedingClient().Object);

        await publisher.PublishAsync(WeatherChanged(), TestContext.Current.CancellationToken);

        var measurement = _telemetry.Measurement(
            PublishCounter,
            ("topic", "weather-changed"),
            ("outcome", TelemetryOutcome.Ok));
        Assert.Equal(1, measurement.Value);

        // The counter is deliberately tagged by topic and outcome only — an event id
        // would add one time series per message published (design D6).
        Assert.Equal(["outcome", "topic"], measurement.Tags.Keys.Order());
    }

    [Fact]
    public async Task PublishAsync_RecordsTheFailure_OnTheSpanAndInTheCounter_ThenRethrows()
    {
        var daprClient = new Mock<DaprClient>();
        daprClient
            .Setup(client => client.PublishEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<WeatherUpdateIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unreachable"));
        var publisher = new DaprIntegrationEventPublisher(daprClient.Object);

        using (_telemetry.Scope())
        {
            // The publisher must not swallow: a caller that believes it published has
            // to fail too.
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => publisher.PublishAsync(
                    new WeatherUpdateIntegrationEvent(21, 3, 60, "None", "Calm", 0.9f),
                    TestContext.Current.CancellationToken));

            Assert.Equal("broker unreachable", exception.Message);
        }

        var activity = _telemetry.Activity(PublishOperation);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("broker unreachable", activity.StatusDescription);
        Assert.Contains(activity.Events, activityEvent => activityEvent.Name == "exception");

        // The topic still reached the span, so a failing topic is searchable.
        Assert.Equal("weather-updated", activity.GetTagItem("messaging.destination.name"));

        var measurement = _telemetry.Measurement(
            PublishCounter,
            ("topic", "weather-updated"),
            ("outcome", TelemetryOutcome.Error));
        Assert.Equal(1, measurement.Value);
    }

    [Fact]
    public async Task PublishAsync_NestsItsSpan_UnderTheWorkThatCausedIt()
    {
        var publisher = new DaprIntegrationEventPublisher(SucceedingClient().Object);

        using var scope = _telemetry.Scope();

        // Publishing from the same shared source makes the ambient activity the parent
        // automatically, which is what gives the end-to-end trace (design D7).
        using (var causingWork = BoogaBoosterTelemetry.ActivitySource.StartActivity("CausingWork"))
        {
            Assert.NotNull(causingWork);
            await publisher.PublishAsync(WeatherChanged(), TestContext.Current.CancellationToken);

            var published = _telemetry.Activity(PublishOperation);
            Assert.Equal(causingWork.SpanId, published.ParentSpanId);
            Assert.Equal(causingWork.TraceId, published.TraceId);
        }
    }

    [Fact]
    public async Task PublishAsync_StartsNoSpan_WhenTheEventDeclaresNoTopic()
    {
        var publisher = new DaprIntegrationEventPublisher(new Mock<DaprClient>().Object);

        using (_telemetry.Scope())
        {
            // The topic is resolved before the span starts, so a contract violation
            // cannot leave a span with no destination on it.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => publisher.PublishAsync(new UntopicedEvent(), TestContext.Current.CancellationToken));
        }

        Assert.Empty(_telemetry.Activities(PublishOperation));
    }

    private static WeatherChangedIntegrationEvent WeatherChanged()
        => new("Amsterdam", 21, "Warm", DateTimeOffset.UnixEpoch);

    private static Mock<DaprClient> SucceedingClient()
    {
        var daprClient = new Mock<DaprClient>();
        daprClient
            .Setup(client => client.PublishEventAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<WeatherChangedIntegrationEvent>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return daprClient;
    }

    private sealed record UntopicedEvent : IIntegrationEvent;
}
