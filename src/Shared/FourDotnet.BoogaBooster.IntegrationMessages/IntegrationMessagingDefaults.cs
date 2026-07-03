namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// Shared constants for the integration-messaging infrastructure. The
/// <see cref="PubSubName"/> value must match the Dapr pub/sub component name
/// registered in the Aspire AppHost.
/// </summary>
public static class IntegrationMessagingDefaults
{
    /// <summary>
    /// The name of the Dapr pub/sub component. Kept in sync with the component
    /// registered via the Aspire Community Toolkit for Dapr in the AppHost.
    /// </summary>
    public const string PubSubName = "pubsub";
}
