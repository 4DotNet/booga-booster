namespace FourDotnet.BoogaBooster.Queue;

/// <summary>
/// Configuration for the Queue module's background filler. Bound from
/// configuration section <see cref="SectionName"/>; the defaults enqueue between
/// four and eight people per minute in varied group sizes.
/// </summary>
public sealed class QueueModuleOptions
{
    public const string SectionName = "Queue";

    /// <summary>The rides whose queues the background filler maintains.</summary>
    public IReadOnlyList<Guid> RideIds { get; set; } = [Guid.Parse("11111111-1111-1111-1111-111111111111")];

    /// <summary>How often the filler runs a fill cycle. Defaults to one minute.</summary>
    public TimeSpan FillInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Minimum number of people to add across a single fill cycle.</summary>
    public int MinArrivalsPerCycle { get; set; } = 4;

    /// <summary>Maximum number of people to add across a single fill cycle.</summary>
    public int MaxArrivalsPerCycle { get; set; } = 8;

    /// <summary>Smallest group size the filler produces (a lone guest is a group of one).</summary>
    public int MinGroupSize { get; set; } = 1;

    /// <summary>Largest group size the filler produces.</summary>
    public int MaxGroupSize { get; set; } = 5;

    /// <summary>Maximum number of people allowed to wait in a single ride's queue.</summary>
    public int MaxQueueLength { get; set; } = 500;

    /// <summary>
    /// Optional fixed seed for the arrival randomness. Set for deterministic
    /// behaviour (tests); leave null to use a time-varying source in production.
    /// </summary>
    public int? RandomSeed { get; set; }

    /// <summary>
    /// The <c>NiceWeather</c> indicator (in <c>[0, 1]</c>) assumed before any
    /// weather-update event has been received. Defaults to <c>1.0</c> so that,
    /// together with the default <see cref="WeatherMultiplierCeiling"/> of
    /// <c>1.0</c>, the pre-event fill rate matches the unscaled base rate — the
    /// weather neither suppresses nor inflates arrivals until real weather arrives.
    /// </summary>
    public double NeutralWeatherDefault { get; set; } = 1.0;

    /// <summary>
    /// Exponent controlling how sharply low <c>NiceWeather</c> suppresses arrivals.
    /// The multiplier is <c>Ceiling * NiceWeather^Exponent</c>, so values above
    /// <c>1.0</c> bend the response down harder as the weather worsens while still
    /// reaching the ceiling at <c>NiceWeather = 1</c>. Must be positive; defaults
    /// to <c>1.0</c> (a linear response).
    /// </summary>
    public double WeatherSuppressionExponent { get; set; } = 1.0;

    /// <summary>
    /// The multiplier applied to the base arrival count at <c>NiceWeather = 1</c>.
    /// Defaults to <c>1.0</c> (cap the fill at the base rate). Raise above
    /// <c>1.0</c> to let very nice weather burst arrivals above the base rate.
    /// </summary>
    public double WeatherMultiplierCeiling { get; set; } = 1.0;
}
