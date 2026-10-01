namespace FourDotnet.BoogaBooster.Queue.Domain;

/// <summary>
/// How a long wait in the queue wears down a guest's happiness. For the first
/// <see cref="GracePeriod"/> nothing happens; beyond it the loss grows
/// exponentially with the extra waiting time <c>t</c>:
/// <c>loss = <see cref="DecayScale"/> × (e^(t / <see cref="TimeConstant"/>) − 1)</c>.
/// The result is clamped at zero. It is a pure function of the starting happiness
/// and the time waited, so it is computed whenever the queue is read rather than
/// ticked by a background loop.
/// </summary>
/// <remarks>
/// With these values a guest loses ~8.6 points after 10 minutes, ~32 after 15 and
/// ~40 after 16 — so a typical guest (starting in [65, 85]) turns mad (below 30)
/// after roughly 15½ to 17½ minutes of waiting.
/// </remarks>
public static class QueueWaitDecay
{
    /// <summary>How long a guest waits before their happiness starts to drop.</summary>
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Scale of the loss in happiness points: one <see cref="TimeConstant"/> past the
    /// grace period a guest has lost <c>DecayScale × (e − 1)</c> ≈ 8.6 points.
    /// </summary>
    public const double DecayScale = 5d;

    /// <summary>The time over which the exponential loss grows by a factor of e.</summary>
    public static readonly TimeSpan TimeConstant = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The happiness a guest who started at <paramref name="startingHappiness"/>
    /// reports after waiting <paramref name="waited"/>, clamped to
    /// [<see cref="Person.MinMood"/>, <paramref name="startingHappiness"/>].
    /// </summary>
    public static double Apply(double startingHappiness, TimeSpan waited)
    {
        var overdue = waited - GracePeriod;
        if (overdue <= TimeSpan.Zero)
        {
            return startingHappiness;
        }

        var loss = DecayScale * (Math.Exp(overdue / TimeConstant) - 1d);
        return Math.Max(Person.MinMood, startingHappiness - loss);
    }
}
