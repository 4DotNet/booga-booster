using System.Collections.Generic;

namespace FourDotnet.BoogaBooster.Core;

/// <summary>
/// Base class for rich domain models (ADR-0003). Holds the lifecycle-state
/// plumbing so concrete models contain only their own properties, validation and
/// intent-revealing operations. A model constructed in code starts <see
/// cref="DomainModelState.New"/>; a model rehydrated from a store starts <see
/// cref="DomainModelState.Pristine"/>. <c>SetX()</c> functions apply changes
/// through <see cref="ApplyChange{T}"/> (single value) or <see cref="MarkChanged"/>
/// (multi-value / collection mutation), which moves the state along.
/// </summary>
public abstract class DomainModel
{
    /// <param name="isNew">
    /// <c>true</c> when the model is being created in code (<see
    /// cref="DomainModelState.New"/>); <c>false</c> when it is being rehydrated
    /// from a data store (<see cref="DomainModelState.Pristine"/>).
    /// </param>
    protected DomainModel(bool isNew)
    {
        State = isNew ? DomainModelState.New : DomainModelState.Pristine;
    }

    /// <summary>The model's current lifecycle state.</summary>
    public DomainModelState State { get; private set; }

    /// <summary>
    /// Applies a single-value change on behalf of a <c>SetX()</c> function. Assigns
    /// only when the value actually differs, and moves the state to
    /// <see cref="DomainModelState.Modified"/> (real change) or
    /// <see cref="DomainModelState.Touched"/> (no-op) accordingly.
    /// </summary>
    /// <returns><c>true</c> when the value actually changed.</returns>
    protected bool ApplyChange<T>(ref T field, T value, IEqualityComparer<T>? comparer = null)
    {
        comparer ??= EqualityComparer<T>.Default;
        var changed = !comparer.Equals(field, value);
        if (changed)
        {
            field = value;
        }

        MarkChanged(changed);
        return changed;
    }

    /// <summary>
    /// Records that a change was applied outside <see cref="ApplyChange{T}"/> — for
    /// example a validated value object or a collection mutation an aggregate owns.
    /// Pass <c>false</c> when the operation turned out to be a no-op.
    /// </summary>
    protected void MarkChanged(bool changed = true)
    {
        // New and Deleted are terminal for change tracking: a not-yet-persisted
        // model stays an insert, and a deleted model stays deleted.
        if (State is DomainModelState.New or DomainModelState.Deleted)
        {
            return;
        }

        if (changed)
        {
            State = DomainModelState.Modified;
        }
        else if (State == DomainModelState.Pristine)
        {
            State = DomainModelState.Touched;
        }
    }

    /// <summary>Marks the model deleted.</summary>
    protected void MarkDeleted()
    {
        State = DomainModelState.Deleted;
    }
}
