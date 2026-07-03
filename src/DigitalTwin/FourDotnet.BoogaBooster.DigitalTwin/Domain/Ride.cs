using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// The ride aggregate root (ADR-0003). Every command — set power, board a
/// passenger, work a brake, start, stop — goes through here so the safety
/// invariants live in one place: the ride can only start when every occupied
/// restraint is secured, the combined passenger weight is within the maximum safe
/// load, and the load is balanced. It owns the whole rig through the
/// <see cref="GreatMill"/> and advances the simulation one fixed step at a time.
/// </summary>
public sealed class Ride : DomainModel
{
    private readonly GreatMill _mill = new();
    private RideState _state = RideState.Idle;
    private double _simulationTimeSeconds;

    private Ride()
        : base(isNew: true)
    {
    }

    /// <summary>Creates a fresh, idle, empty ride.</summary>
    public static Ride Create() => new();

    /// <summary>The ride's lifecycle state (distinct from the base persistence state).</summary>
    public RideState CurrentState => _state;

    /// <summary>Simulated time elapsed since the twin started (seconds).</summary>
    public double SimulationTimeSeconds => _simulationTimeSeconds;

    /// <summary>The central mill (and, through it, the hubs and gondolas).</summary>
    public GreatMill Mill => _mill;

    /// <summary>Why the ride is (not) safe to start right now.</summary>
    public RideSafetyReason SafetyReason => EvaluateSafety();

    /// <summary><c>true</c> when <see cref="Start"/> would succeed right now.</summary>
    public bool IsSafeToStart =>
        _state is RideState.Idle or RideState.Boarding or RideState.Ready
        && EvaluateSafety() == RideSafetyReason.None;

    /// <summary>Sets the main (mill) engine power.</summary>
    public bool SetMainEnginePower(EnginePower power)
    {
        ArgumentNullException.ThrowIfNull(power);
        var changed = _mill.SetPower(power);
        MarkChanged(changed);
        return changed;
    }

    /// <summary>
    /// Sets the hub engine power. All four hubs receive the same power; their speeds
    /// still differ because their loads differ.
    /// </summary>
    public bool SetHubEnginePower(EnginePower power)
    {
        ArgumentNullException.ThrowIfNull(power);
        var changed = _mill.SetAllHubPower(power);
        MarkChanged(changed);
        return changed;
    }

    /// <summary>Boards a passenger into a specific seat, with a natural restraint-close delay.</summary>
    public void BoardPassenger(
        int hubIndex,
        int gondolaIndex,
        SeatPosition seat,
        Passenger passenger,
        TimeSpan restraintCloseDelay)
    {
        ArgumentNullException.ThrowIfNull(passenger);

        if (_state is not (RideState.Idle or RideState.Boarding or RideState.Ready))
        {
            throw new DomainValidationException($"Passengers can only board while the ride is idle or boarding (state: {_state}).");
        }

        _mill.GetHub(hubIndex).GetGondola(gondolaIndex).Board(seat, passenger, restraintCloseDelay);
        _state = RideState.Boarding;
        MarkChanged();
    }

    /// <summary>Engages or releases a specific gondola's yaw brake.</summary>
    public void SetGondolaBrake(int hubIndex, int gondolaIndex, GondolaBrakeState brake)
    {
        var gondola = _mill.GetHub(hubIndex).GetGondola(gondolaIndex);
        if (brake == GondolaBrakeState.Engaged)
        {
            gondola.EngageBrake();
        }
        else
        {
            gondola.ReleaseBrake();
        }

        MarkChanged();
    }

    /// <summary>
    /// Starts the ride. Releases the gondola brakes so they can swing freely, and
    /// begins spinning.
    /// </summary>
    /// <exception cref="DomainValidationException">
    /// Thrown when the ride is not in a startable state, an occupied restraint is not
    /// secured, or the load is unbalanced.
    /// </exception>
    public void Start()
    {
        if (_state is not (RideState.Idle or RideState.Boarding or RideState.Ready))
        {
            throw new DomainValidationException($"The ride cannot start from state {_state}.");
        }

        var reason = EvaluateSafety();
        if (reason != RideSafetyReason.None)
        {
            throw new DomainValidationException($"The ride is not safe to start: {DescribeSafety(reason)}");
        }

        _mill.ReleaseAllGondolaBrakes();
        _state = RideState.Running;
        MarkChanged();
    }

    /// <summary>Begins a controlled ramp down: cuts power and coasts to a stop.</summary>
    public void Stop()
    {
        if (_state is not (RideState.Running or RideState.Ready))
        {
            return;
        }

        _mill.CutAllPower();
        _state = _state == RideState.Ready ? RideState.Idle : RideState.Stopping;
        MarkChanged();
    }

    /// <summary>
    /// Advances the whole simulation one fixed step: natural passenger behaviour,
    /// readiness re-evaluation, and — while running or stopping — the physics.
    /// </summary>
    public void Advance(TimeSpan dt)
    {
        if (dt <= TimeSpan.Zero)
        {
            throw new DomainValidationException("The simulation step must be positive.");
        }

        _mill.AdvanceNaturalBehavior(dt);
        UpdateReadiness();

        if (_state is RideState.Running or RideState.Stopping)
        {
            _mill.AdvancePhysics(dt.TotalSeconds);

            if (_state == RideState.Stopping && _mill.IsAtRest)
            {
                _mill.EngageAllGondolaBrakes();
                _state = RideState.Idle;
            }
        }

        _simulationTimeSeconds += dt.TotalSeconds;
        MarkChanged();
    }

    /// <summary>Builds an immutable snapshot of the whole ride.</summary>
    public RideTelemetry ToTelemetry()
    {
        var reason = EvaluateSafety();
        var isSafeToStart =
            _state is RideState.Idle or RideState.Boarding or RideState.Ready
            && reason == RideSafetyReason.None;

        var hubs = new List<HubTelemetry>(RideParameters.HubCount);
        var gondolas = new List<GondolaTelemetry>(RideParameters.HubCount * RideParameters.GondolasPerHub);
        foreach (var hub in _mill.Hubs)
        {
            hubs.Add(hub.ToTelemetry());
            foreach (var gondola in hub.Gondolas)
            {
                gondolas.Add(gondola.ToTelemetry());
            }
        }

        return new RideTelemetry(
            _state,
            _simulationTimeSeconds,
            isSafeToStart,
            reason,
            _mill.ToTelemetry(),
            hubs,
            gondolas);
    }

    private void UpdateReadiness()
    {
        switch (_state)
        {
            case RideState.Boarding when EvaluateSafety() == RideSafetyReason.None:
                _state = RideState.Ready;
                break;
            case RideState.Ready when EvaluateSafety() != RideSafetyReason.None:
                _state = RideState.Boarding;
                break;
        }
    }

    private RideSafetyReason EvaluateSafety()
    {
        foreach (var hub in _mill.Hubs)
        {
            foreach (var gondola in hub.Gondolas)
            {
                if (!gondola.IsSafeToDispatch)
                {
                    return RideSafetyReason.UnsecuredRestraint;
                }
            }
        }

        if (_mill.IsOverloaded)
        {
            return RideSafetyReason.Overloaded;
        }

        if (!_mill.IsBalanced)
        {
            return RideSafetyReason.UnbalancedLoad;
        }

        return RideSafetyReason.None;
    }

    private static string DescribeSafety(RideSafetyReason reason) => reason switch
    {
        RideSafetyReason.UnsecuredRestraint => "an occupied seat's restraint is not secured.",
        RideSafetyReason.UnbalancedLoad => "the load is distributed too unevenly across the arms.",
        RideSafetyReason.Overloaded => $"the combined passenger weight exceeds the {RideParameters.MaxPassengerLoadKg:0} kg maximum load.",
        RideSafetyReason.WrongState => "the ride is not in a startable state.",
        _ => "unknown reason.",
    };
}
