namespace FourDotnet.BoogaBooster.Core;

/// <summary>
/// Thrown by a domain model when a value fails validation inside an
/// intent-revealing <c>SetX()</c> function or a value-object constructor
/// (ADR-0003). Signals a broken invariant, not an infrastructure failure.
/// </summary>
public sealed class DomainValidationException : Exception
{
    public DomainValidationException(string message)
        : base(message)
    {
    }

    public DomainValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
