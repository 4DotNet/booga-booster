namespace FourDotnet.BoogaBooster.Core;

/// <summary>
/// The lifecycle state of a <see cref="DomainModel"/> (ADR-0003). The state is a
/// reliable signal for change tracking and persistence: it distinguishes a
/// freshly created model from a rehydrated one, and a real change from a no-op.
/// </summary>
public enum DomainModelState
{
    /// <summary>Newly created in memory; does not yet exist in the data store.</summary>
    New,

    /// <summary>Materialized (read) from the data store and unchanged since.</summary>
    Pristine,

    /// <summary>A <c>SetX()</c> function ran but the value did not actually change.</summary>
    Touched,

    /// <summary>A <c>SetX()</c> function ran that actually changed a value.</summary>
    Modified,

    /// <summary>The model's <c>Delete()</c> function was called.</summary>
    Deleted
}
