namespace FourDotnet.BoogaBooster.Core.Domain;

/// <summary>
/// Thrown when a domain model or value object is asked to enter an invalid
/// state. Validation lives inside the model/value object and runs before any
/// value is assigned (ADR-0003).
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
