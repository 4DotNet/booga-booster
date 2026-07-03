using System.Collections.Concurrent;
using System.Reflection;

namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// Resolves the Dapr topic name declared by an integration event's
/// <see cref="TopicNameAttribute"/>. Results are cached per type so the
/// reflection lookup happens only once per event type.
/// </summary>
public static class TopicNameResolver
{
    private static readonly ConcurrentDictionary<Type, string> TopicCache = new();

    /// <summary>
    /// Returns the topic name declared by the <see cref="TopicNameAttribute"/>
    /// on <paramref name="eventType"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the type is not decorated with a <see cref="TopicNameAttribute"/>.
    /// </exception>
    public static string Resolve(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        return TopicCache.GetOrAdd(eventType, static type =>
        {
            var attribute = type.GetCustomAttribute<TopicNameAttribute>(inherit: false);
            if (attribute is null)
            {
                throw new InvalidOperationException(
                    $"Integration event '{type.FullName}' is missing a [TopicName(\"...\")] attribute. " +
                    "Every integration event must declare the Dapr topic it maps to.");
            }

            return attribute.Name;
        });
    }

    /// <summary>Returns the topic name declared on <typeparamref name="TEvent"/>.</summary>
    public static string Resolve<TEvent>()
        where TEvent : IIntegrationEvent
        => Resolve(typeof(TEvent));
}
