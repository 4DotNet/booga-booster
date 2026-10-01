using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the rider: a <see cref="Passenger"/> carries the experience it boarded with,
/// and changes it only through its intent methods.
/// </summary>
public sealed class PassengerTests
{
    [Fact]
    public void A_passenger_created_from_a_boarding_guest_carries_the_guests_weight_and_experience()
    {
        var experience = new PassengerExperience(60d, 90d, 10d);

        var passenger = Passenger.From(new BoardingPassenger(new PassengerWeight(82d), experience));

        Assert.Equal(82d, passenger.Weight.Kilograms);
        Assert.Equal(experience, passenger.Experience);
    }

    [Fact]
    public void A_passenger_of_a_given_weight_has_the_default_experience()
    {
        Assert.Equal(PassengerExperience.Default, Passenger.OfWeight(70d).Experience);
    }

    [Fact]
    public void Riding_a_step_evolves_the_experience()
    {
        var passenger = new Passenger(new PassengerWeight(75d), new PassengerExperience(50d, 50d, 0d));

        passenger.ExperienceRideStep(intensity: 50d, dtSeconds: 2d);

        Assert.Equal(new PassengerExperience(50d, 50d, 0d).AfterRideStep(50d, 2d), passenger.Experience);
    }

    [Fact]
    public void Suffering_sustained_max_g_adds_the_penalty()
    {
        var passenger = new Passenger(new PassengerWeight(75d), new PassengerExperience(50d, 50d, 5d));

        passenger.SufferSustainedMaxG();

        Assert.Equal(5d + RideParameters.MaxGNauseaPenalty, passenger.Experience.Nausea);
    }

    [Fact]
    public void A_passenger_requires_a_weight_and_an_experience()
    {
        Assert.Throws<ArgumentNullException>(() => new Passenger(null!, PassengerExperience.Default));
        Assert.Throws<ArgumentNullException>(() => new Passenger(new PassengerWeight(75d), null!));
        Assert.Throws<ArgumentNullException>(() => Passenger.From(null!));
    }

    [Fact]
    public void A_boarding_guest_requires_a_weight_and_an_experience()
    {
        Assert.Throws<ArgumentNullException>(() => new BoardingPassenger(null!, PassengerExperience.Default));
        Assert.Throws<ArgumentNullException>(() => new BoardingPassenger(new PassengerWeight(75d), null!));
    }
}
