using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the <see cref="RiderExperience"/> value object and its attachment to a
/// <see cref="Passenger"/> (docs/06 §6.1): validated construction, clamping mutators,
/// and the default experience of a rider without a queue history.
/// </summary>
public sealed class RiderExperienceTests
{
    [Theory]
    [InlineData(RideParameters.ExperienceScaleMin)]
    [InlineData(42.5)]
    [InlineData(RideParameters.ExperienceScaleMax)]
    public void RiderExperience_accepts_values_on_the_scale(double value)
    {
        var experience = new RiderExperience(value, value, value);

        Assert.Equal(value, experience.Happiness);
        Assert.Equal(value, experience.PreferredRideIntensity);
        Assert.Equal(value, experience.Nausea);
    }

    [Theory]
    [InlineData(-0.1, 50, 0)]
    [InlineData(100.1, 50, 0)]
    [InlineData(double.NaN, 50, 0)]
    [InlineData(70, -0.1, 0)]
    [InlineData(70, 100.1, 0)]
    [InlineData(70, double.NaN, 0)]
    [InlineData(70, 50, -0.1)]
    [InlineData(70, 50, 100.1)]
    [InlineData(70, 50, double.PositiveInfinity)]
    public void RiderExperience_rejects_values_off_the_scale(double happiness, double preference, double nausea)
    {
        Assert.Throws<DomainValidationException>(() => new RiderExperience(happiness, preference, nausea));
    }

    [Fact]
    public void RaiseHappiness_adds_the_points_and_leaves_the_other_values()
    {
        var raised = new RiderExperience(70, 60, 5).RaiseHappiness(12.5);

        Assert.Equal(new RiderExperience(82.5, 60, 5), raised);
    }

    [Fact]
    public void RaiseHappiness_is_held_at_the_top_of_the_scale()
    {
        var raised = new RiderExperience(95, 60, 5).RaiseHappiness(20);

        Assert.Equal(RideParameters.ExperienceScaleMax, raised.Happiness);
    }

    [Fact]
    public void AddNausea_is_held_at_the_top_of_the_scale()
    {
        var sick = new RiderExperience(70, 60, 90).AddNausea(25);

        Assert.Equal(RideParameters.ExperienceScaleMax, sick.Nausea);
        Assert.Equal(70, sick.Happiness);
    }

    [Fact]
    public void Mutators_return_a_new_value_and_leave_the_original_untouched()
    {
        var original = new RiderExperience(70, 60, 0);

        _ = original.RaiseHappiness(10).AddNausea(10);

        Assert.Equal(new RiderExperience(70, 60, 0), original);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Mutators_reject_a_negative_or_undefined_rise(double points)
    {
        var experience = new RiderExperience(70, 60, 0);

        Assert.Throws<DomainValidationException>(() => experience.RaiseHappiness(points));
        Assert.Throws<DomainValidationException>(() => experience.AddNausea(points));
    }

    [Fact]
    public void Default_is_the_midpoint_of_the_queue_arrival_bands_with_no_nausea()
    {
        Assert.Equal(
            new RiderExperience(
                RideParameters.DefaultRiderHappiness,
                RideParameters.DefaultRiderPreferredIntensity,
                RideParameters.ExperienceScaleMin),
            RiderExperience.Default);
    }

    [Fact]
    public void A_default_struct_is_a_valid_all_zero_experience()
    {
        var zero = default(RiderExperience);

        Assert.Equal(new RiderExperience(0, 0, 0), zero);
    }

    [Fact]
    public void Passenger_without_a_queue_history_boards_with_the_default_experience()
    {
        Assert.Equal(RiderExperience.Default, Passenger.OfWeight(80).Experience);
    }

    [Fact]
    public void Passenger_boards_with_the_experience_it_is_given()
    {
        var experience = new RiderExperience(60, 90, 10);

        var passenger = new Passenger(new PassengerWeight(80), experience);

        Assert.Equal(experience, passenger.Experience);
    }

    [Fact]
    public void Passenger_SetExperience_replaces_the_experience()
    {
        var passenger = Passenger.OfWeight(80);
        var updated = passenger.Experience.RaiseHappiness(5);

        passenger.SetExperience(updated);

        Assert.Equal(updated, passenger.Experience);
    }
}
