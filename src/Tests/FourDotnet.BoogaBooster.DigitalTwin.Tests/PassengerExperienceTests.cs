using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the <see cref="PassengerExperience"/> value object and its evolution rules
/// (docs/06-passenger-experience.md §6.1, §6.4–6.6): validation on construction,
/// clamping on change, the Gaussian happiness gain, the exact-step nausea growth past
/// a 30-point excess, and the sustained-max-G penalty.
/// </summary>
public sealed class PassengerExperienceTests
{
    /// <summary><c>r_n / λ_n</c>, the shift in the closed-form nausea solution.</summary>
    private const double NauseaShift = RideParameters.NauseaBaseRate / RideParameters.NauseaGrowthRate;

    private static PassengerExperience Rider(double happiness = 50d, double preferredIntensity = 50d, double nausea = 0d) =>
        new(happiness, preferredIntensity, nausea);

    // --- Validation and clamping (§6.1) ---

    [Theory]
    [InlineData(RideParameters.MinExperienceRating)]
    [InlineData(RideParameters.MaxExperienceRating)]
    public void Ratings_at_the_bounds_are_accepted(double rating)
    {
        var experience = new PassengerExperience(rating, rating, rating);

        Assert.Equal(rating, experience.Happiness);
        Assert.Equal(rating, experience.PreferredIntensity);
        Assert.Equal(rating, experience.Nausea);
    }

    [Theory]
    [InlineData(120d, 50d, 0d)]
    [InlineData(-1d, 50d, 0d)]
    [InlineData(50d, 100.5d, 0d)]
    [InlineData(50d, -0.5d, 0d)]
    [InlineData(50d, 50d, 101d)]
    [InlineData(50d, 50d, double.NaN)]
    public void An_out_of_range_rating_is_rejected(double happiness, double preferredIntensity, double nausea)
    {
        Assert.Throws<DomainValidationException>(() => new PassengerExperience(happiness, preferredIntensity, nausea));
    }

    [Fact]
    public void Raising_nausea_beyond_the_maximum_clamps_it()
    {
        var experience = Rider(nausea: 90d).WithNausea(90d + 25d);

        Assert.Equal(RideParameters.MaxExperienceRating, experience.Nausea);
    }

    [Fact]
    public void Lowering_happiness_below_the_minimum_clamps_it()
    {
        var experience = Rider(happiness: 10d).WithHappiness(-5d);

        Assert.Equal(RideParameters.MinExperienceRating, experience.Happiness);
    }

    [Fact]
    public void A_clamped_change_keeps_the_other_ratings()
    {
        var experience = Rider(happiness: 60d, preferredIntensity: 80d, nausea: 5d).WithHappiness(70d);

        Assert.Equal(new PassengerExperience(70d, 80d, 5d), experience);
    }

    [Fact]
    public void The_default_experience_is_the_mean_arrival_with_no_nausea()
    {
        Assert.Equal(
            new PassengerExperience(
                RideParameters.DefaultRiderHappiness,
                RideParameters.DefaultRiderPreferredIntensity,
                RideParameters.MinExperienceRating),
            PassengerExperience.Default);
    }

    // --- Happiness (§6.4) ---

    [Fact]
    public void A_moderate_rider_gains_more_on_a_moderate_ride_than_on_a_wild_one()
    {
        var rider = Rider(happiness: 50d, preferredIntensity: 50d);

        var matched = rider.AfterRideStep(intensity: 50d, dtSeconds: 10d);
        var tooWild = rider.AfterRideStep(intensity: 80d, dtSeconds: 10d);

        Assert.True(matched.Happiness > tooWild.Happiness, $"{matched.Happiness} should exceed {tooWild.Happiness}.");
    }

    [Fact]
    public void A_thrill_seeker_gains_more_at_maximum_intensity_than_at_half()
    {
        var rider = Rider(happiness: 50d, preferredIntensity: 100d);

        var atMax = rider.AfterRideStep(intensity: 100d, dtSeconds: 1d);
        var atHalf = rider.AfterRideStep(intensity: 50d, dtSeconds: 1d);

        Assert.True(atMax.Happiness > atHalf.Happiness, $"{atMax.Happiness} should exceed {atHalf.Happiness}.");
    }

    [Fact]
    public void A_perfect_match_gains_at_the_peak_rate()
    {
        var experience = Rider(happiness: 50d, preferredIntensity: 50d).AfterRideStep(intensity: 50d, dtSeconds: 10d);

        Assert.Equal(50d + (RideParameters.HappinessGainRate * 10d), experience.Happiness, precision: 12);
    }

    [Fact]
    public void The_happiness_gain_is_symmetric_in_the_mismatch()
    {
        var rider = Rider(happiness: 50d, preferredIntensity: 60d);

        var tooTame = rider.AfterRideStep(intensity: 48d, dtSeconds: 5d);
        var tooWild = rider.AfterRideStep(intensity: 72d, dtSeconds: 5d);

        Assert.Equal(tooTame.Happiness, tooWild.Happiness, precision: 12);
    }

    [Fact]
    public void Happiness_saturates_at_the_maximum()
    {
        var experience = Rider(happiness: 99d, preferredIntensity: 50d).AfterRideStep(intensity: 50d, dtSeconds: 60d);

        Assert.Equal(RideParameters.MaxExperienceRating, experience.Happiness);
    }

    [Fact]
    public void No_ride_step_ever_lowers_happiness_or_nausea()
    {
        for (var preferred = 0d; preferred <= 100d; preferred += 10d)
        {
            for (var intensity = 0d; intensity <= 100d; intensity += 5d)
            {
                var before = Rider(happiness: 40d, preferredIntensity: preferred, nausea: 20d);

                var after = before.AfterRideStep(intensity, dtSeconds: 1d);

                Assert.True(after.Happiness >= before.Happiness, $"Happiness fell at I={intensity}, P={preferred}.");
                Assert.True(after.Nausea >= before.Nausea, $"Nausea fell at I={intensity}, P={preferred}.");
                Assert.Equal(before.PreferredIntensity, after.PreferredIntensity);
            }
        }
    }

    // --- Nausea (§6.5) ---

    [Fact]
    public void An_excess_just_below_the_threshold_leaves_nausea_unchanged()
    {
        var experience = Rider(preferredIntensity: 50d, nausea: 10d).AfterRideStep(intensity: 79d, dtSeconds: 10d);

        Assert.Equal(10d, experience.Nausea);
    }

    [Fact]
    public void An_excess_at_the_threshold_grows_nausea()
    {
        var experience = Rider(preferredIntensity: 50d, nausea: 10d).AfterRideStep(intensity: 80d, dtSeconds: 1d);

        Assert.True(experience.Nausea > 10d, $"Nausea should grow at a 30-point excess but was {experience.Nausea}.");
    }

    [Fact]
    public void Nausea_follows_the_closed_form_exact_step()
    {
        const double dt = 10d;

        var experience = Rider(preferredIntensity: 50d, nausea: 0d).AfterRideStep(intensity: 90d, dtSeconds: dt);

        var expected = (NauseaShift * Math.Exp(RideParameters.NauseaGrowthRate * dt)) - NauseaShift;
        Assert.Equal(expected, experience.Nausea, precision: 12);
        Assert.Equal(23.2d, experience.Nausea, precision: 1); // docs/06 §6.5 table
    }

    [Fact]
    public void Sustained_excess_accelerates_over_equal_intervals()
    {
        var start = Rider(preferredIntensity: 50d, nausea: 0d);

        var afterFirst = start.AfterRideStep(intensity: 90d, dtSeconds: 3d);
        var afterSecond = afterFirst.AfterRideStep(intensity: 90d, dtSeconds: 3d);

        var firstGain = afterFirst.Nausea - start.Nausea;
        var secondGain = afterSecond.Nausea - afterFirst.Nausea;
        Assert.True(firstGain > 0d);
        Assert.True(secondGain > firstGain, $"Second interval ({secondGain}) should add more than the first ({firstGain}).");
    }

    [Fact]
    public void Nausea_saturates_at_the_maximum()
    {
        var experience = Rider(preferredIntensity: 50d, nausea: 95d).AfterRideStep(intensity: 100d, dtSeconds: 30d);

        Assert.Equal(RideParameters.MaxExperienceRating, experience.Nausea);
    }

    [Fact]
    public void The_step_is_exact_regardless_of_step_size()
    {
        const int steps = 600;
        var dt = TestHelpers.Dt.TotalSeconds;
        var start = Rider(happiness: 20d, preferredIntensity: 60d, nausea: 0d);
        var oneStep = start.AfterRideStep(intensity: 95d, dtSeconds: steps * dt);

        var manySteps = start;
        for (var i = 0; i < steps; i++)
        {
            manySteps = manySteps.AfterRideStep(intensity: 95d, dtSeconds: dt);
        }

        Assert.Equal(oneStep.Happiness, manySteps.Happiness, precision: 9);
        Assert.Equal(oneStep.Nausea, manySteps.Nausea, precision: 9);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(100.1d)]
    [InlineData(double.NaN)]
    public void An_out_of_range_intensity_is_rejected(double intensity)
    {
        Assert.Throws<DomainValidationException>(() => Rider().AfterRideStep(intensity, dtSeconds: 1d));
    }

    [Theory]
    [InlineData(-0.001d)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NaN)]
    public void An_invalid_step_is_rejected(double dtSeconds)
    {
        Assert.Throws<DomainValidationException>(() => Rider().AfterRideStep(intensity: 50d, dtSeconds));
    }

    // --- Sustained max-G penalty (§6.6) ---

    [Fact]
    public void A_max_g_penalty_adds_the_fixed_nausea()
    {
        var experience = Rider(nausea: 10d).WithMaxGPenalty();

        Assert.Equal(10d + RideParameters.MaxGNauseaPenalty, experience.Nausea);
    }

    [Fact]
    public void A_max_g_penalty_is_clamped_at_the_maximum()
    {
        var experience = Rider(nausea: 90d).WithMaxGPenalty();

        Assert.Equal(RideParameters.MaxExperienceRating, experience.Nausea);
    }
}
