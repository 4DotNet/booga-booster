namespace FourDotnet.BoogaBooster.Core.Observability;

/// <summary>
/// Derives the span/metric operation name for a handler from its type name, so
/// <c>GetQueueStatusQueryHandler</c> reports as <c>GetQueueStatus</c> without every
/// handler repeating its own name as a literal.
/// </summary>
internal static class HandlerOperationName
{
    private static readonly string[] Suffixes = ["CommandHandler", "QueryHandler", "Handler"];

    internal static string For(Type handlerType)
    {
        var name = handlerType.Name;

        foreach (var suffix in Suffixes)
        {
            if (name.Length > suffix.Length && name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return name[..^suffix.Length];
            }
        }

        return name;
    }
}
