using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// How a waiting guest's happiness wears down (ADR-0003 value object, design D2).
/// Happiness holds for a <see cref="GracePeriod"/>, then decays exponentially,
/// halving every <see cref="HalfLife"/>:
/// <code>
/// H(w) = H₀                                 for w ≤ grace
/// H(w) = H₀ · 2^(−(w − grace) / halfLife)   for w &gt; grace
/// </code>
/// The function is pure — it depends only on the starting happiness and the wait —
/// so a queue status read at any instant is exact without a background ticker.
/// </summary>
public sealed record HappinessDecay
{
    /// <param name="gracePeriod">How long a guest waits before their mood starts to drop. Must not be negative.</param>
    /// <param name="halfLife">How long it takes for happiness to halve once the grace period is over. Must be positive.</param>
    /// <exception cref="DomainValidationException">Either duration is out of range.</exception>
    public HappinessDecay(TimeSpan gracePeriod, TimeSpan halfLife)
    {
        if (gracePeriod < TimeSpan.Zero)
        {
            throw new DomainValidationException("The happiness grace period cannot be negative.");
        }

        if (halfLife <= TimeSpan.Zero)
        {
            throw new DomainValidationException("The happiness half-life must be positive.");
        }

        GracePeriod = gracePeriod;
        HalfLife = halfLife;
    }

    /// <summary>How long happiness holds before it starts to decay.</summary>
    public TimeSpan GracePeriod { get; }

    /// <summary>How long it takes for happiness to halve once the grace period is over.</summary>
    public TimeSpan HalfLife { get; }

    /// <summary>
    /// The happiness of a guest who arrived with <paramref name="initialHappiness"/>
    /// and has now waited <paramref name="waited"/>. Never higher than the starting
    /// value and never below zero.
    /// </summary>
    public double Apply(double initialHappiness, TimeSpan waited)
    {
        var overdue = waited - GracePeriod;
        if (overdue <= TimeSpan.Zero)
        {
            return initialHappiness;
        }

        var halvings = overdue / HalfLife;
        return initialHappiness * Math.Pow(2d, -halvings);
    }
}
