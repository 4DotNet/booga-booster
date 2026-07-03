namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// Marker interface implemented by every integration event. Constrains the
/// publisher so only genuine integration events can be published, and makes the
/// set of contracts in this library discoverable.
/// </summary>
public interface IIntegrationEvent;
