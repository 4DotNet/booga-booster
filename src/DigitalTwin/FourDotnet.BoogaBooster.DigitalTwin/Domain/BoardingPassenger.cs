namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// Everything the ride needs to seat one guest of a boarding group: their body weight
/// and the experience ratings they bring from the queue (design D4). Carrying both in
/// one value keeps weight and experience from drifting apart, as two parallel lists
/// could.
/// </summary>
/// <param name="Weight">The guest's validated body weight.</param>
/// <param name="Experience">The guest's validated experience ratings on boarding.</param>
public sealed record BoardingPassenger(PassengerWeight Weight, PassengerExperience Experience)
{
    /// <summary>The guest's validated body weight.</summary>
    public PassengerWeight Weight { get; } = Weight ?? throw new ArgumentNullException(nameof(Weight));

    /// <summary>The guest's validated experience ratings on boarding.</summary>
    public PassengerExperience Experience { get; } = Experience ?? throw new ArgumentNullException(nameof(Experience));
}
