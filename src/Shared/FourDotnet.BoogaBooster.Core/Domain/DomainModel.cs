namespace FourDotnet.BoogaBooster.Core.Domain;

/// <summary>
/// Base class for rich domain models (ADR-0003). Owns the lifecycle
/// <see cref="State"/> and the <see cref="ApplyChange{T}"/> helper that every
/// intent-revealing <c>SetX()</c> function calls to apply a validated value and
/// transition the state correctly (<see cref="DomainModelState.Touched"/> for a
/// no-op change, <see cref="DomainModelState.Modified"/> for a real change).
/// </summary>
public abstract class DomainModel
{
    /// <param name="isNew">
    /// <c>true</c> when the model is created in memory (<see cref="DomainModelState.New"/>);
    /// <c>false</c> when it is materialized from a data store (<see cref="DomainModelState.Pristine"/>).
    /// </param>
    protected DomainModel(bool isNew)
    {
        State = isNew ? DomainModelState.New : DomainModelState.Pristine;
    }

    /// <summary>The current lifecycle state of this model.</summary>
    public DomainModelState State { get; private set; }

    /// <summary>
    /// Applies a change through the base-class mechanism. Compares the current
    /// value with the proposed value; if they are equal the change is a no-op
    /// (the state becomes <see cref="DomainModelState.Touched"/> and the value is
    /// not reassigned); otherwise the value is assigned and the state becomes
    /// <see cref="DomainModelState.Modified"/>. Validation for the value must run
    /// in the calling <c>SetX()</c> function <em>before</em> calling this.
    /// </summary>
    /// <returns><c>true</c> when the value actually changed; otherwise <c>false</c>.</returns>
    protected bool ApplyChange<T>(
        T current,
        T proposed,
        Action<T> assign,
        IEqualityComparer<T>? comparer = null)
    {
        ArgumentNullException.ThrowIfNull(assign);

        comparer ??= EqualityComparer<T>.Default;

        if (comparer.Equals(current, proposed))
        {
            MarkTouched();
            return false;
        }

        assign(proposed);
        MarkModified();
        return true;
    }

    /// <summary>Marks the model as deleted.</summary>
    public void Delete() => State = DomainModelState.Deleted;

    private void MarkTouched()
    {
        // A no-op never downgrades a New or already-Modified model, and it only
        // moves a Pristine model to Touched.
        if (State == DomainModelState.Pristine)
        {
            State = DomainModelState.Touched;
        }
    }

    private void MarkModified()
    {
        // A model created in memory stays New until it is persisted; a real
        // change to a materialized model marks it Modified.
        if (State != DomainModelState.New)
        {
            State = DomainModelState.Modified;
        }
    }
}
