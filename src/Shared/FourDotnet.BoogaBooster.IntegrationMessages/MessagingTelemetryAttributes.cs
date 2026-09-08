namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// The span attribute names the integration-event publisher records. The transport
/// attributes follow the OpenTelemetry messaging semantic conventions so a trace
/// backend recognises them; the event-type attribute is ours, because no convention
/// covers it. Declared as constants in one place so a rename is a single edit and
/// tests assert against the same name production code writes.
/// </summary>
internal static class MessagingTelemetryAttributes
{
    /// <summary>The messaging system carrying the event — always <c>dapr</c> here.</summary>
    internal const string System = "messaging.system";

    /// <summary>The resolved topic the event was published to.</summary>
    internal const string DestinationName = "messaging.destination.name";

    /// <summary>The Dapr pub/sub component the event was handed to.</summary>
    internal const string ComponentName = "messaging.dapr.component";

    /// <summary>The runtime type name of the published event.</summary>
    internal const string EventType = "messaging.event.type";

    /// <summary>
    /// Metric tag: the topic published to. A bounded set — one value per declared
    /// integration event — so it is safe as a tag, unlike an event id.
    /// </summary>
    internal const string TopicTag = "topic";

    /// <summary>The value <see cref="System"/> always takes.</summary>
    internal const string DaprSystem = "dapr";
}
