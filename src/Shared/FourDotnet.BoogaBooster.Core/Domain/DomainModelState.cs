namespace FourDotnet.BoogaBooster.Core.Domain;

/// <summary>
/// Lifecycle state of a <see cref="DomainModel"/> (ADR-0003). The initial state
/// may only be <see cref="New"/> or <see cref="Pristine"/>; it then flows as
/// <c>SetX()</c> and <see cref="DomainModel.Delete"/> operations are applied.
/// </summary>
public enum DomainModelState
{
    /// <summary>Newly created in memory; does not yet exist in a data store.</summary>
    New,

    /// <summary>Materialized from a data store and unchanged since.</summary>
    Pristine,

    /// <summary>A <c>SetX()</c> ran but the value did not actually change.</summary>
    Touched,

    /// <summary>A <c>SetX()</c> ran that actually changed a value.</summary>
    Modified,

    /// <summary>The model's <see cref="DomainModel.Delete"/> was called.</summary>
    Deleted,
}
