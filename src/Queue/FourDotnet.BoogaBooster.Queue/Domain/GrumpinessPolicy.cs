using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// How a wait in the line erodes a guest's happiness (ADR-0003 value object). For as
/// long as a group has waited no longer than <see cref="Onset"/>, happiness is
/// unchanged; every minute beyond it costs <see cref="RatePerMinute"/>, floored at
/// <see cref="RiderProfile.MinHappiness"/>. The erosion is a pure, closed-form
/// function of the wait — nothing mutates the stored profile and no second clock
/// runs in the module — so the reported value never depends on scheduler timing.
/// Built once from <see cref="QueueModuleOptions"/> and held by every
/// <see cref="RideQueue"/>.
/// </summary>
public sealed record GrumpinessPolicy
{
    public GrumpinessPolicy(TimeSpan onset, double ratePerMinute)
    {
        if (onset < TimeSpan.Zero)
        {
            throw new DomainValidationException("Grumpiness onset cannot be negative.");
        }

        if (!double.IsFinite(ratePerMinute) || ratePerMinute < 0)
        {
            throw new DomainValidationException("Grumpiness rate per minute must be a finite, non-negative number.");
        }

        Onset = onset;
        RatePerMinute = ratePerMinute;
    }

    /// <summary>How long a group may wait before its members start losing happiness.</summary>
    public TimeSpan Onset { get; }

    /// <summary>Happiness lost per minute waited beyond <see cref="Onset"/>.</summary>
    public double RatePerMinute { get; }

    /// <summary>
    /// The happiness a guest who arrived with <paramref name="initialHappiness"/>
    /// reports after having waited <paramref name="waited"/>:
    /// <c>clamp(initial − rate × max(0, waited − onset) in minutes, 0, 1)</c>.
    /// Exactly at the onset the value is still the initial one.
    /// </summary>
    public double CurrentHappiness(double initialHappiness, TimeSpan waited)
    {
        var beyondOnset = waited - Onset;
        if (beyondOnset <= TimeSpan.Zero)
        {
            return initialHappiness;
        }

        return Math.Clamp(
            initialHappiness - (RatePerMinute * beyondOnset.TotalMinutes),
            RiderProfile.MinHappiness,
            RiderProfile.MaxHappiness);
    }
}
