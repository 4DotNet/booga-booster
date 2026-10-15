using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A single rider the twin accepts as a load. A passenger is a validated body
/// weight plus how they are getting on — their <see cref="RiderExperience"/>. Where
/// they sit and whether their restraint is secured is owned by the
/// <see cref="Seat"/> they board.
/// </summary>
public sealed class Passenger : DomainModel
{
    private RiderExperience _experience;

    /// <summary>Creates a passenger with the default experience of a rider without a queue history.</summary>
    public Passenger(PassengerWeight weight)
        : this(weight, RiderExperience.Default)
    {
    }

    /// <summary>Creates a passenger who boards with the given <paramref name="experience"/>.</summary>
    public Passenger(PassengerWeight weight, RiderExperience experience)
        : base(isNew: true)
    {
        ArgumentNullException.ThrowIfNull(weight);
        Weight = weight;
        _experience = experience;
    }

    /// <summary>The passenger's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>The passenger's current happiness, preferred ride intensity and nausea.</summary>
    public RiderExperience Experience => _experience;

    /// <summary>Creates a passenger of the given weight in kilograms.</summary>
    public static Passenger OfWeight(double kilograms) => new(new PassengerWeight(kilograms));

    /// <summary>
    /// Replaces the passenger's experience. The value object is already valid, so there
    /// is nothing left to check; an unchanged value only touches the model.
    /// </summary>
    public void SetExperience(RiderExperience experience) => ApplyChange(ref _experience, experience);
}
