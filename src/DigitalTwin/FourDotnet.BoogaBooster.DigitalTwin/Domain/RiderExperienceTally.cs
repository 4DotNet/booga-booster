using FourDotnet.BoogaBooster.DigitalTwin.Abstractions.DataTransferObjects;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// Accumulates the experience of everyone on board into the telemetry summary
/// (design D7): the rider count and the average happiness, preferred intensity and
/// nausea, with the averages absent when nobody is aboard. A stack-only value that
/// sums as it goes, so building a frame allocates nothing beyond the summary itself.
/// </summary>
internal struct RiderExperienceTally
{
    private int _riders;
    private double _happiness;
    private double _preferredIntensity;
    private double _nausea;

    /// <summary>Counts a seat's rider; an empty seat (<c>null</c>) is skipped.</summary>
    public void Add(RiderExperience? rider)
    {
        if (rider is not { } experience)
        {
            return;
        }

        _riders++;
        _happiness += experience.Happiness;
        _preferredIntensity += experience.PreferredRideIntensity;
        _nausea += experience.Nausea;
    }

    /// <summary>The summary of everyone counted so far.</summary>
    public readonly RiderExperienceTelemetry ToTelemetry() =>
        _riders == 0
            ? new RiderExperienceTelemetry(0, null, null, null)
            : new RiderExperienceTelemetry(
                _riders,
                _happiness / _riders,
                _preferredIntensity / _riders,
                _nausea / _riders);
}
