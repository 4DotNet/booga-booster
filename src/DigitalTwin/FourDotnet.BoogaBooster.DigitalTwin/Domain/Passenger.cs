using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A single rider the twin accepts as a load. A passenger is just a validated body
/// weight — where they sit and whether their restraint is secured is owned by the
/// <see cref="Seat"/> they board.
/// </summary>
public sealed class Passenger : DomainModel
{
    public Passenger(PassengerWeight weight)
        : base(isNew: true)
    {
        ArgumentNullException.ThrowIfNull(weight);
        Weight = weight;
    }

    /// <summary>The passenger's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>Creates a passenger of the given weight in kilograms.</summary>
    public static Passenger OfWeight(double kilograms) => new(new PassengerWeight(kilograms));
}
