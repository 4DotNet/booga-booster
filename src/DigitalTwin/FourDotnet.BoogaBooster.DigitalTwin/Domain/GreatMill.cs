using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// The central motor-driven spindle carrying the four hubs (a driven body of
/// <c>docs/02-rotational-dynamics.md</c>). Besides power, speed, and load, it
/// reports a <em>load balance</em> reading: how far the combined centre of mass sits
/// off the spin axis when the four arms are loaded unevenly.
/// </summary>
public sealed class GreatMill : DomainModel
{
    private readonly Hub[] _hubs;
    private EnginePower _power = EnginePower.Off;
    private double _angle;
    private double _omega;

    public GreatMill()
        : base(isNew: true)
    {
        _hubs = new Hub[RideParameters.HubCount];
        for (var i = 0; i < _hubs.Length; i++)
        {
            _hubs[i] = new Hub(i);
        }
    }

    /// <summary>The commanded motor power.</summary>
    public EnginePower Power => _power;

    /// <summary>Current rotation angle (rad).</summary>
    public double Angle => _angle;

    /// <summary>Current rotation speed (rad/s).</summary>
    public double AngularVelocity => _omega;

    /// <summary>The four hubs.</summary>
    public IReadOnlyList<Hub> Hubs => _hubs;

    /// <summary>Total rotating gondola + passenger load carried by the mill (its "load weight").</summary>
    public double LoadKg
    {
        get
        {
            var sum = 0d;
            foreach (var hub in _hubs)
            {
                sum += hub.LoadKg;
            }

            return sum;
        }
    }

    /// <summary>Combined weight of every seated passenger across the ride (kg), excluding gondola structure.</summary>
    public double PassengerLoadKg
    {
        get
        {
            var sum = 0d;
            foreach (var hub in _hubs)
            {
                sum += hub.PassengerLoadKg;
            }

            return sum;
        }
    }

    /// <summary>
    /// <c>false</c> when the combined passenger weight exceeds the safe maximum — an
    /// overloaded-ride fault. The load must never exceed
    /// <see cref="RideParameters.MaxPassengerLoadKg"/>.
    /// </summary>
    public bool IsOverloaded => PassengerLoadKg > RideParameters.MaxPassengerLoadKg;

    /// <summary>Electrical power the mill motor is consuming (watts).</summary>
    public double ConsumedPowerWatts => _power.ToWatts(RideParameters.MillMaxPowerWatts);

    /// <summary>Moment of inertia about the central axis (kg·m²), recomputed from the current load.</summary>
    public double Inertia
    {
        get
        {
            var arms = RideParameters.HubCount
                * (RideParameters.MillArmKg * RideParameters.MillArmLength * RideParameters.MillArmLength / 3d);

            var assemblies = 0d;
            foreach (var hub in _hubs)
            {
                assemblies += hub.AssemblyMass * RideParameters.MillArmLength * RideParameters.MillArmLength;
            }

            return arms + assemblies;
        }
    }

    /// <summary>
    /// How far the mill's combined centre of mass sits off the spin axis (metres).
    /// Zero when opposite arms carry matching loads; it grows with lopsided loading.
    /// </summary>
    public double ImbalanceMeters
    {
        get
        {
            var totalMass = 0d;
            var moment = PlanarVector.Zero;
            for (var i = 0; i < _hubs.Length; i++)
            {
                var mass = _hubs[i].AssemblyMass;
                totalMass += mass;
                moment += PlanarVector.FromAngle(RideKinematics.MountAngle(i)) * (mass * RideParameters.MillArmLength);
            }

            return totalMass <= 0d ? 0d : (moment * (1d / totalMass)).Length;
        }
    }

    /// <summary>The load-balance reading in millimetres.</summary>
    public double ImbalanceMillimeters => ImbalanceMeters * 1000d;

    /// <summary><c>false</c> when the imbalance exceeds the safe limit — an unbalanced-load fault.</summary>
    public bool IsBalanced => ImbalanceMillimeters <= RideParameters.MaxImbalanceMillimeters;

    /// <summary><c>true</c> when the mill and every hub have effectively stopped.</summary>
    public bool IsAtRest
    {
        get
        {
            if (Math.Abs(_omega) >= 1e-3d)
            {
                return false;
            }

            foreach (var hub in _hubs)
            {
                if (!hub.IsAtRest)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary><c>true</c> when every gondola across the ride is empty.</summary>
    public bool IsEmpty
    {
        get
        {
            foreach (var hub in _hubs)
            {
                if (!hub.IsEmpty)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>Returns the hub at <paramref name="index"/>.</summary>
    public Hub GetHub(int index)
    {
        if (index < 0 || index >= _hubs.Length)
        {
            throw new DomainValidationException($"Hub index must be between 0 and {_hubs.Length - 1}.");
        }

        return _hubs[index];
    }

    /// <summary>Sets the mill motor power (validated by the value object).</summary>
    public bool SetPower(EnginePower power)
    {
        ArgumentNullException.ThrowIfNull(power);
        return ApplyChange(ref _power, power);
    }

    /// <summary>Sets the same commanded power on all four hub motors.</summary>
    public bool SetAllHubPower(EnginePower power)
    {
        ArgumentNullException.ThrowIfNull(power);

        var changed = false;
        foreach (var hub in _hubs)
        {
            changed |= hub.SetPower(power);
        }

        return changed;
    }

    /// <summary>Cuts power to the mill and every hub (the ramp-down source when stopping).</summary>
    public void CutAllPower()
    {
        SetPower(EnginePower.Off);
        SetAllHubPower(EnginePower.Off);
    }

    /// <summary>Advances natural passenger behaviour across the whole ride.</summary>
    public void AdvanceNaturalBehavior(TimeSpan elapsed)
    {
        foreach (var hub in _hubs)
        {
            hub.AdvanceNaturalBehavior(elapsed);
        }
    }

    /// <summary>Advances the mill one physics step, then every hub (and its gondolas).</summary>
    public void AdvancePhysics(double dt)
    {
        var drive = RotationalDynamics.MotorTorque(
            _power.Fraction, _omega, RideParameters.MillStallTorque, RideParameters.MillMaxPowerWatts);

        var step = RotationalDynamics.Integrate(
            _omega,
            _angle,
            drive,
            RideParameters.MillCoulombFriction,
            RideParameters.MillViscousFriction,
            RideParameters.MillAeroDrag,
            Inertia,
            RideParameters.MillMaxAngularVelocity,
            dt);

        _omega = step.Omega;
        _angle = step.Theta;

        foreach (var hub in _hubs)
        {
            hub.AdvancePhysics(_angle, _omega, dt);
        }
    }

    /// <summary>Releases every gondola brake across the ride.</summary>
    public void ReleaseAllGondolaBrakes()
    {
        foreach (var hub in _hubs)
        {
            hub.ReleaseAllBrakes();
        }
    }

    /// <summary>Engages every gondola brake across the ride.</summary>
    public void EngageAllGondolaBrakes()
    {
        foreach (var hub in _hubs)
        {
            hub.EngageAllBrakes();
        }
    }

    /// <summary>Releases every gondola's safety restraints across the ride (offloading).</summary>
    public void ReleaseAllRestraints()
    {
        foreach (var hub in _hubs)
        {
            hub.ReleaseAllRestraints();
        }
    }

    /// <summary>Lets passengers leave every gondola across the ride.</summary>
    public void Offload()
    {
        foreach (var hub in _hubs)
        {
            hub.Offload();
        }
    }

    /// <summary>Total rotational kinetic energy of the mill and all hubs (joules).</summary>
    public double KineticEnergy()
    {
        var energy = RotationalDynamics.KineticEnergy(Inertia, _omega);
        foreach (var hub in _hubs)
        {
            energy += RotationalDynamics.KineticEnergy(hub.Inertia, hub.AngularVelocity);
        }

        return energy;
    }

    /// <summary>Projects the mill onto the telemetry DTO.</summary>
    public MillTelemetry ToTelemetry() => new(
        ConsumedPowerWatts,
        RotationalDynamics.ToRpm(_omega),
        LoadKg,
        PassengerLoadKg,
        ImbalanceMillimeters,
        IsBalanced,
        IsOverloaded);
}
