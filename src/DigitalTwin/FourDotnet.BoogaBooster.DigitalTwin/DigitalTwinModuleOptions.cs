namespace FourDotnet.BoogaBooster.DigitalTwin;

/// <summary>
/// Configuration for the DigitalTwin module. Bound from configuration section
/// <see cref="SectionName"/>.
/// </summary>
public sealed class DigitalTwinModuleOptions
{
    public const string SectionName = "DigitalTwin";

    /// <summary>
    /// Identifies the ride whose waiting line the loading coordinator drains while
    /// the ride is loading. This must match the ride id the Queue module fills — its
    /// default is the same well-known value — otherwise the coordinator finds an
    /// empty queue and nobody ever boards.
    /// </summary>
    public Guid RideId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");
}
