namespace FourDotnet.BoogaBooster.Core.Observability;

/// <summary>
/// The tag and attribute names shared across modules — the ones the CQRS base
/// classes write and that more than one module's instruments reuse. Declared here
/// for the same reason each module has its own registry: a name written as a
/// literal at the call site cannot be renamed in one edit.
/// </summary>
/// <remarks>
/// Module-specific vocabulary does <em>not</em> belong here; it lives in the owning
/// module's <c>Observability/&lt;Module&gt;TelemetryAttributes.cs</c>. This class
/// holds only what is genuinely shared.
/// </remarks>
public static class TelemetryTags
{
    /// <summary>Span attribute: the handler operation the activity covers.</summary>
    public const string SpanOperation = "boogabooster.operation";

    /// <summary>Span attribute: whether the operation is a command or a query.</summary>
    public const string SpanOperationKind = "boogabooster.operation.kind";

    /// <summary>Metric tag: the handler operation. A bounded set — one per handler.</summary>
    public const string Operation = "operation";

    /// <summary>Metric tag: <c>command</c> or <c>query</c>.</summary>
    public const string Kind = "kind";

    /// <summary>
    /// Metric tag: how the work ended. Its values are <see cref="TelemetryOutcome"/>,
    /// and every instrument that reports success or failure uses this key so a
    /// dashboard can filter them all the same way.
    /// </summary>
    public const string Outcome = "outcome";
}
