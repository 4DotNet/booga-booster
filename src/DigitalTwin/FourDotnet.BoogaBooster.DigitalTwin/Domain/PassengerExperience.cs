using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// A rider's three experience ratings — <see cref="Happiness"/>,
/// <see cref="PreferredIntensity"/> and <see cref="Nausea"/> — as one validated value
/// object (ADR-0003), with the pure evolution rules of
/// <c>docs/06-passenger-experience.md</c> §6.4–6.6. Every rating lies in
/// [<see cref="RideParameters.MinExperienceRating"/>, <see cref="RideParameters.MaxExperienceRating"/>]:
/// construction rejects anything outside it, while evolution clamps to it.
/// </summary>
/// <remarks>
/// Every update is closed-form, so the result is exact for any step size and does not
/// drift if the physics timestep ever changes (design D1).
/// </remarks>
public sealed record PassengerExperience
{
    /// <summary><c>r_n / λ_n</c> — the shift that turns the nausea ODE into pure exponential growth.</summary>
    private const double NauseaShift = RideParameters.NauseaBaseRate / RideParameters.NauseaGrowthRate;

    public PassengerExperience(double happiness, double preferredIntensity, double nausea)
    {
        EnsureRating(happiness, nameof(Happiness));
        EnsureRating(preferredIntensity, nameof(PreferredIntensity));
        EnsureRating(nausea, nameof(Nausea));

        Happiness = happiness;
        PreferredIntensity = preferredIntensity;
        Nausea = nausea;
    }

    /// <summary>
    /// The experience of a passenger boarded without a queue record: the mean arrival
    /// happiness and preferred intensity, and no nausea.
    /// </summary>
    public static PassengerExperience Default { get; } = new(
        RideParameters.DefaultRiderHappiness,
        RideParameters.DefaultRiderPreferredIntensity,
        RideParameters.MinExperienceRating);

    /// <summary>How happy the rider is (0 very sad, 100 extremely happy).</summary>
    public double Happiness { get; }

    /// <summary>The ride intensity the rider prefers (0 tamest, 100 most intense).</summary>
    public double PreferredIntensity { get; }

    /// <summary>How nauseous the rider is (0 none, 100 maximum).</summary>
    public double Nausea { get; }

    /// <summary>A copy with <see cref="Happiness"/> set to <paramref name="value"/>, clamped to the rating range.</summary>
    public PassengerExperience WithHappiness(double value) =>
        new(Clamp(value), PreferredIntensity, Nausea);

    /// <summary>A copy with <see cref="Nausea"/> set to <paramref name="value"/>, clamped to the rating range.</summary>
    public PassengerExperience WithNausea(double value) =>
        new(Happiness, PreferredIntensity, Clamp(value));

    /// <summary>
    /// The experience after riding <paramref name="dtSeconds"/> at the experienced
    /// <paramref name="intensity"/> (docs/06 §6.4–6.5):
    /// <list type="bullet">
    /// <item>happiness rises at <c>k_h·e^(−(Δ/σ_h)²)</c> with <c>Δ = intensity − preference</c>
    /// — never falls;</item>
    /// <item>nausea grows by the exact step <c>N' = (N + r_n/λ_n)·e^(λ_n·dt) − r_n/λ_n</c>, but
    /// only while <c>Δ ≥ NauseaExcessThreshold</c> — never falls.</item>
    /// </list>
    /// Both results are clamped to the rating range.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// <paramref name="intensity"/> is outside the rating range, or
    /// <paramref name="dtSeconds"/> is negative or not finite.
    /// </exception>
    public PassengerExperience AfterRideStep(double intensity, double dtSeconds)
    {
        if (!(intensity is >= RideParameters.MinExperienceRating and <= RideParameters.MaxExperienceRating))
        {
            throw new DomainValidationException(
                $"Experienced intensity must be between {RideParameters.MinExperienceRating} and {RideParameters.MaxExperienceRating}.");
        }

        if (!double.IsFinite(dtSeconds) || dtSeconds < 0d)
        {
            throw new DomainValidationException("The ride step must be a finite, non-negative number of seconds.");
        }

        var excess = intensity - PreferredIntensity;

        var mismatch = excess / RideParameters.HappinessMatchWidth;
        var happinessRate = RideParameters.HappinessGainRate * Math.Exp(-(mismatch * mismatch));
        var happiness = Clamp(Happiness + (happinessRate * dtSeconds));

        var nausea = excess >= RideParameters.NauseaExcessThreshold
            ? Clamp(((Nausea + NauseaShift) * Math.Exp(RideParameters.NauseaGrowthRate * dtSeconds)) - NauseaShift)
            : Nausea;

        // Skip the allocation when the step changed nothing (e.g. both ratings saturated):
        // this runs for every rider at the physics rate.
        return happiness == Happiness && nausea == Nausea
            ? this
            : new PassengerExperience(happiness, PreferredIntensity, nausea);
    }

    /// <summary>
    /// The experience after one sustained max-G episode: nausea rises by
    /// <see cref="RideParameters.MaxGNauseaPenalty"/>, clamped at the maximum (docs/06 §6.6).
    /// </summary>
    public PassengerExperience WithMaxGPenalty() => WithNausea(Nausea + RideParameters.MaxGNauseaPenalty);

    private static double Clamp(double value) =>
        Math.Clamp(value, RideParameters.MinExperienceRating, RideParameters.MaxExperienceRating);

    private static void EnsureRating(double value, string rating)
    {
        // The negated range check also rejects NaN, which compares false to everything.
        if (!(value is >= RideParameters.MinExperienceRating and <= RideParameters.MaxExperienceRating))
        {
            throw new DomainValidationException(
                $"Passenger {rating} must be between {RideParameters.MinExperienceRating} and {RideParameters.MaxExperienceRating}.");
        }
    }
}
