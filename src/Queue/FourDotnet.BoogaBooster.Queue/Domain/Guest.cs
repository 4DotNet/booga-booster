using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// A single guest waiting for a ride. Guests have identity only in this slice;
/// group membership and boarding order are owned by <see cref="QueuedGroup"/> and
/// <see cref="RideQueue"/>.
/// </summary>
public sealed class Guest : DomainModel
{
    public Guest(Guid id)
        : base(isNew: true)
    {
        if (id == Guid.Empty)
        {
            throw new DomainValidationException("Guest id is required.");
        }

        Id = id;
    }

    public Guid Id { get; private set; }

    /// <summary>Creates a guest with a freshly generated identity.</summary>
    public static Guest CreateNew() => new(Guid.NewGuid());
}
