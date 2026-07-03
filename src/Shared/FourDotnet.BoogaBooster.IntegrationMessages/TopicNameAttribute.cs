namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// Declares the Dapr pub/sub topic an integration event is published to and
/// subscribed from. Every integration event must be decorated with this
/// attribute; the topic value is the single source of truth shared by the
/// publisher and every <c>WithTopic(...)</c> subscriber.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TopicNameAttribute : Attribute
{
    public TopicNameAttribute(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A topic name must be provided.", nameof(name));
        }

        Name = name;
    }

    /// <summary>The name of the Dapr pub/sub topic.</summary>
    public string Name { get; }
}
