using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// Running totals for the rider-mood roll-up (design D9): how many riders are seated
/// and the sum of their happiness and nausea. A mutable struct threaded by
/// <c>ref</c> down the mill → hub → gondola chain so the 30 Hz telemetry projection
/// walks the thirty-two seats without allocating.
/// </summary>
internal struct RiderMoodSum
{
    private int _count;
    private double _happiness;
    private double _nausea;

    /// <summary>Adds one seated passenger's current mood to the totals.</summary>
    public void Add(Passenger passenger)
    {
        _count++;
        _happiness += passenger.Happiness;
        _nausea += passenger.Nausea;
    }

    /// <summary>The roll-up: the count, and the averages — <c>null</c> when nobody is seated.</summary>
    public readonly RiderMoodTelemetry ToTelemetry() =>
        _count == 0
            ? new RiderMoodTelemetry(0, null, null)
            : new RiderMoodTelemetry(_count, _happiness / _count, _nausea / _count);
}
