using FourDotnet.BoogaBooster.Queue.Domain;

namespace FourDotnet.BoogaBooster.Queue.Filling;

/// <summary>
/// Produces the actual people who arrive at a ride: each with a process-unique
/// number, a generated name and a realistically distributed weight.
/// </summary>
public interface IPersonGenerator
{
    /// <summary>Creates the next person with a fresh unique number.</summary>
    Person Next();

    /// <summary>
    /// Creates a single arriving group of <paramref name="size"/> freshly generated
    /// people under a new group identity.
    /// </summary>
    /// <exception cref="FourDotnet.BoogaBooster.Core.DomainValidationException">
    /// <paramref name="size"/> is less than one.
    /// </exception>
    GroupArrival CreateGroup(int size);
}
