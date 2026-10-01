using FourDotnet.BoogaBooster.Core;

namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// The queue-patience policy: how waiting erodes a guest's happiness
/// (<c>docs/06-passenger-experience.md</c> §6.2). The first
/// <see cref="GracePeriod"/> of waiting is free; past it, happiness decays
/// exponentially with the extra waiting time,
/// <c>H(w) = H₀ · e^(−(w − GracePeriod) / DecayTimeConstant)</c>.
/// </summary>
/// <remarks>
/// A pure function of arrival happiness and elapsed time, so the queue evaluates it
/// at read time instead of ticking every waiting guest (design D3): the result is
/// exact whenever it is read, independent of how often anyone looks.
/// </remarks>
public static class QueuePatience
{
    /// <summary>
    /// <c>w_g</c> (<c>QueuePatienceGrace</c> in the docs) — how long a guest may wait
    /// before their happiness starts to erode.
    /// </summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    /// <summary>
    /// <c>τ_q</c> (<c>QueuePatienceTimeConstant</c> in the docs) — the e-folding time of
    /// the erosion past the grace period: after a further 10 minutes about 37 % of the
    /// arrival happiness is left.
    /// </summary>
    public static readonly TimeSpan DecayTimeConstant = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The happiness of a guest who arrived with <paramref name="arrivalHappiness"/>
    /// after waiting <paramref name="waited"/>, clamped to
    /// [<see cref="Person.MinRating"/>, <see cref="Person.MaxRating"/>].
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// <paramref name="arrivalHappiness"/> is outside the rating range.
    /// </exception>
    public static double HappinessAfter(double arrivalHappiness, TimeSpan waited)
    {
        if (!(arrivalHappiness is >= Person.MinRating and <= Person.MaxRating))
        {
            throw new DomainValidationException(
                $"Arrival happiness must be between {Person.MinRating} and {Person.MaxRating}.");
        }

        // A non-positive wait (a clock read before the join stamp) is no wait at all.
        if (waited <= GracePeriod)
        {
            return arrivalHappiness;
        }

        var overdue = (waited - GracePeriod) / DecayTimeConstant;
        var eroded = arrivalHappiness * Math.Exp(-overdue);

        // The exponential never goes negative; the clamp only guards rounding.
        return Math.Clamp(eroded, Person.MinRating, Person.MaxRating);
    }
}
