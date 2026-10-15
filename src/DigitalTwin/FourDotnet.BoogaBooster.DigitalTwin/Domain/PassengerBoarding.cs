namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// One member of a group about to board (design D4): their body weight and the
/// experience they bring from the queue — happiness as it stood when they left the
/// line, their preferred ride intensity and their nausea. Both parts are validated
/// value objects, so a boarding can never carry an invalid rider.
/// </summary>
public sealed record PassengerBoarding
{
    public PassengerBoarding(PassengerWeight weight, RiderExperience experience)
    {
        ArgumentNullException.ThrowIfNull(weight);

        Weight = weight;
        Experience = experience;
    }

    /// <summary>The rider's body weight.</summary>
    public PassengerWeight Weight { get; }

    /// <summary>The experience the rider boards with.</summary>
    public RiderExperience Experience { get; }

    /// <summary>The seated passenger this boarding becomes.</summary>
    public Passenger ToPassenger() => new(Weight, Experience);
}
