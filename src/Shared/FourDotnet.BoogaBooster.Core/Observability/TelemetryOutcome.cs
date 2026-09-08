namespace FourDotnet.BoogaBooster.Core.Observability;

/// <summary>
/// The bounded set of values the <c>outcome</c> metric tag takes. Declared once so
/// every instrument that reports success or failure agrees on the spelling and a
/// dashboard can filter on it (ADR-0009's low-cardinality rule).
/// </summary>
public static class TelemetryOutcome
{
    /// <summary>The work completed.</summary>
    public const string Ok = "ok";

    /// <summary>The work threw.</summary>
    public const string Error = "error";

    /// <summary>A guarded request the domain refused — a failure, but an expected one.</summary>
    public const string Rejected = "rejected";

    /// <summary>A guarded request the domain allowed.</summary>
    public const string Accepted = "accepted";
}
