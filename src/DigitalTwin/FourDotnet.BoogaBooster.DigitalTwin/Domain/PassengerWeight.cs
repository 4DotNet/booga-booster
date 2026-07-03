using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A passenger's body weight, validated on construction (ADR-0003). A person weighs
/// between <see cref="RideParameters.MinPassengerKg"/> and
/// <see cref="RideParameters.MaxPassengerKg"/> kilograms.
/// </summary>
public sealed record PassengerWeight
{
    public PassengerWeight(double kilograms)
    {
        if (double.IsNaN(kilograms) || double.IsInfinity(kilograms))
        {
            throw new DomainValidationException("Passenger weight must be a finite number of kilograms.");
        }

        if (kilograms < RideParameters.MinPassengerKg || kilograms > RideParameters.MaxPassengerKg)
        {
            throw new DomainValidationException(
                $"Passenger weight must be between {RideParameters.MinPassengerKg} and {RideParameters.MaxPassengerKg} kg.");
        }

        Kilograms = kilograms;
    }

    /// <summary>The weight in kilograms.</summary>
    public double Kilograms { get; }
}
