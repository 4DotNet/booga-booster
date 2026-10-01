using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// Running sums of the riders' experience ratings, built up seat by seat during the
/// telemetry walk and turned into the averaged <see cref="RiderExperienceTelemetry"/>.
/// A mutable struct on the stack, so summarising the ride allocates nothing beyond the
/// one telemetry record — telemetry is built on every simulation step.
/// </summary>
internal struct RiderExperienceTally
{
    private int _riders;
    private double _happiness;
    private double _preferredIntensity;
    private double _nausea;

    /// <summary>Adds the seat's rider, if any, to the tally.</summary>
    public void Add(Seat seat)
    {
        if (seat.Occupant is not { } rider)
        {
            return;
        }

        var experience = rider.Experience;
        _riders++;
        _happiness += experience.Happiness;
        _preferredIntensity += experience.PreferredIntensity;
        _nausea += experience.Nausea;
    }

    /// <summary>The averages over every rider added, or no averages when nobody was.</summary>
    public readonly RiderExperienceTelemetry ToTelemetry() =>
        _riders == 0
            ? RiderExperienceTelemetry.Empty
            : new RiderExperienceTelemetry(
                _happiness / _riders,
                _preferredIntensity / _riders,
                _nausea / _riders);
}
