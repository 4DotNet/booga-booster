using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A single rider the twin accepts as a load: a validated body weight plus the
/// rider's <see cref="Experience"/> — happiness, preferred intensity and nausea — which
/// evolves while the ride runs. Where they sit and whether their restraint is secured
/// is owned by the <see cref="Seat"/> they board.
/// </summary>
public sealed class Passenger : DomainModel
{
    private PassengerExperience _experience;

    public Passenger(PassengerWeight weight, PassengerExperience experience)
        : base(isNew: true)
    {
        ArgumentNullException.ThrowIfNull(weight);
        ArgumentNullException.ThrowIfNull(experience);

        Weight = weight;
        _experience = experience;
    }

    /// <summary>The passenger's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>The passenger's current experience ratings.</summary>
    public PassengerExperience Experience => _experience;

    /// <summary>Creates a passenger of the given weight in kilograms with the <see cref="PassengerExperience.Default"/> experience.</summary>
    public static Passenger OfWeight(double kilograms) => new(new PassengerWeight(kilograms), PassengerExperience.Default);

    /// <summary>Creates the passenger a boarding guest becomes.</summary>
    public static Passenger From(BoardingPassenger boarding)
    {
        ArgumentNullException.ThrowIfNull(boarding);
        return new Passenger(boarding.Weight, boarding.Experience);
    }

    /// <summary>
    /// The rider spends <paramref name="dtSeconds"/> at the experienced
    /// <paramref name="intensity"/>: happiness and nausea evolve per
    /// <see cref="PassengerExperience.AfterRideStep"/>.
    /// </summary>
    public void ExperienceRideStep(double intensity, double dtSeconds) =>
        ApplyChange(ref _experience, _experience.AfterRideStep(intensity, dtSeconds));

    /// <summary>
    /// The rider's gondola held the maximum G-force for a whole episode: nausea rises by
    /// the fixed penalty, per <see cref="PassengerExperience.WithMaxGPenalty"/>.
    /// </summary>
    public void SufferSustainedMaxG() =>
        ApplyChange(ref _experience, _experience.WithMaxGPenalty());
}
