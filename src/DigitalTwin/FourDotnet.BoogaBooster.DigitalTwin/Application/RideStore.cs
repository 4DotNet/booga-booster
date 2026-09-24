using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Default <see cref="IRideStore"/>. Guards the single <see cref="Ride"/> aggregate
/// with a lock so the simulation loop (writer), the read query, and the operator
/// commands never see or produce a half-updated state. Also serves the cross-module
/// <see cref="IRideTelemetryProvider"/>.
/// </summary>
public sealed class RideStore : IRideStore, IRideTelemetryProvider
{
    private readonly Lock _gate = new();
    private readonly IRideEventSampler _sampler;
    private readonly Ride _ride = Ride.Create();

    public RideStore(IRideEventSampler sampler)
    {
        _sampler = sampler ?? throw new ArgumentNullException(nameof(sampler));
    }

    public RideTelemetry GetTelemetry()
    {
        lock (_gate)
        {
            return _ride.ToTelemetry();
        }
    }

    public bool IsActive
    {
        get
        {
            lock (_gate)
            {
                return _ride.IsActive;
            }
        }
    }

    public RideState CurrentState
    {
        get
        {
            lock (_gate)
            {
                return _ride.CurrentState;
            }
        }
    }

    public int EmptyGondolaCount
    {
        get
        {
            lock (_gate)
            {
                return _ride.EmptyGondolaCount;
            }
        }
    }

    public RideTelemetry Advance(TimeSpan dt)
    {
        IReadOnlyList<Passenger> departed;
        RideTelemetry telemetry;

        lock (_gate)
        {
            departed = _ride.Advance(dt);
            telemetry = _ride.ToTelemetry();
        }

        // Recorded outside the lock: the meter is not part of the ride's consistency
        // boundary, and the departed list is already a private snapshot.
        RecordFinalMood(departed);
        return telemetry;
    }

    /// <summary>
    /// Records each leaving rider's final happiness and nausea in the two offload
    /// histograms (design D10). Untagged, and never a name or a seat: the distribution
    /// is what an operator trends, and ADR-0009 keeps personal data off metrics.
    /// </summary>
    private static void RecordFinalMood(IReadOnlyList<Passenger> departed)
    {
        for (var i = 0; i < departed.Count; i++)
        {
            var passenger = departed[i];
            BoogaBoosterTelemetry.RiderFinalHappiness.Record(passenger.Happiness);
            BoogaBoosterTelemetry.RiderFinalNausea.Record(passenger.Nausea);
        }
    }

    public RideTelemetry SetMainEnginePower(EnginePower power)
    {
        lock (_gate)
        {
            _ride.SetMainEnginePower(power);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry SetHubEnginePower(EnginePower power)
    {
        lock (_gate)
        {
            _ride.SetHubEnginePower(power);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry SetMainEngineDirection(MotorDirection direction)
    {
        lock (_gate)
        {
            _ride.SetMainEngineDirection(direction);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry SetHubEngineDirection(MotorDirection direction)
    {
        lock (_gate)
        {
            _ride.SetHubEngineDirection(direction);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry BoardPassenger(int hubIndex, int gondolaIndex, SeatPosition seat, PassengerWeight? weight)
    {
        lock (_gate)
        {
            var passenger = new Passenger(weight ?? _sampler.NextPassengerWeight(), _sampler.NextRiderProfile());
            var delay = _sampler.NextRestraintCloseDelay();
            _ride.BoardPassenger(hubIndex, gondolaIndex, seat, passenger, delay);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry BoardGroup(IReadOnlyList<Passenger> members)
    {
        ArgumentNullException.ThrowIfNull(members);

        lock (_gate)
        {
            _ride.BoardGroup(members, _sampler.NextRestraintCloseDelay, _sampler.NextGondolaSelection);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry SetGondolaBrake(int hubIndex, int gondolaIndex, GondolaBrakeState brake)
    {
        lock (_gate)
        {
            _ride.SetGondolaBrake(hubIndex, gondolaIndex, brake);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry SetEngineBrakes(bool engaged)
    {
        lock (_gate)
        {
            _ride.SetEngineBrakes(engaged);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry RequestStateTransition(RideState target)
    {
        lock (_gate)
        {
            _ride.RequestTransition(target);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry StartRide()
    {
        lock (_gate)
        {
            _ride.RequestTransition(RideState.Started);
            return _ride.ToTelemetry();
        }
    }

    public RideTelemetry StopRide()
    {
        lock (_gate)
        {
            _ride.RequestTransition(RideState.Stopping);
            return _ride.ToTelemetry();
        }
    }
}
