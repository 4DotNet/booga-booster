namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// The single home for the twin's physical constants and tuning values — geometry,
/// masses, motor curves, losses, and safety limits. Keeping them together makes the
/// simulation safe to tune, and mirrors the derivations in <c>docs/</c>. All values
/// are SI unless the name says otherwise.
/// </summary>
public static class RideParameters
{
    // --- Passengers (spec: a person weighs between 30 and 150 kg) ---
    public const double MinPassengerKg = 30d;
    public const double MaxPassengerKg = 150d;
    public const double DefaultPassengerKg = 75d;

    // --- Geometry (metres) ---
    public const double MillArmLength = 6d;
    public const double HubArmLength = 2d;
    public const double SeatLateralOffset = 0.5d;

    // --- Masses (kg) ---
    public const double EmptyGondolaKg = 150d;
    public const double HubStructureKg = 400d;
    public const double MillArmKg = 800d;
    public const double HubArmKg = 120d;

    // --- Gondola centre-of-mass model (metres, about the pivot) ---
    /// <summary>Pivot→CoM distance of an empty gondola (its back sits behind the pivot).</summary>
    public const double EmptyPivotToComMeters = 0.35d;

    /// <summary>How far behind the pivot a seated passenger's mass sits.</summary>
    public const double PassengerBackOffsetMeters = 0.6d;

    /// <summary>How far to the side of the pivot a seated passenger's mass sits.</summary>
    public const double PassengerLateralOffsetMeters = 0.45d;

    /// <summary>Yaw inertia of an empty gondola about its pivot (kg·m²).</summary>
    public const double EmptyGondolaPivotInertia = 220d;

    /// <summary>Pivot bearing damping (N·m·s/rad) — the "sticky vs free pivot" knob.</summary>
    public const double GondolaPivotDamping = 900d;

    // --- Simulation cadence ---
    /// <summary>The fixed physics timestep (1/120 s) — non-negotiable for determinism.</summary>
    public static readonly TimeSpan TimeStep = TimeSpan.FromSeconds(1d / 120d);

    /// <summary>How often a telemetry snapshot is published.</summary>
    public static readonly TimeSpan TelemetryInterval = TimeSpan.FromSeconds(1d / 30d);

    // --- Mill motor & losses ---
    public const double MillStallTorque = 60_000d;
    public const double MillMaxPowerWatts = 90_000d;
    public const double MillCoulombFriction = 800d;
    public const double MillViscousFriction = 1_500d;
    public const double MillAeroDrag = 5_000d;
    public const double MillMaxAngularVelocity = 2.5d;

    // --- Hub motor & losses ---
    public const double HubStallTorque = 8_000d;
    public const double HubMaxPowerWatts = 15_000d;
    public const double HubCoulombFriction = 100d;
    public const double HubViscousFriction = 200d;
    public const double HubAeroDrag = 150d;
    public const double HubMaxAngularVelocity = 5d;

    /// <summary>Angular-speed floor in the motor curve — avoids divide-by-zero at standstill.</summary>
    public const double OmegaEpsilon = 1e-3d;

    // --- Physics constants ---
    public const double Gravity = 9.81d;

    // --- Safety limits ---
    /// <summary>Maximum felt load before over-G protection intervenes.</summary>
    public const double MaxGForce = 4.5d;

    /// <summary>Maximum mill CoM offset before the load is considered unbalanced.</summary>
    public const double MaxImbalanceMillimeters = 250d;

    /// <summary>
    /// Maximum combined passenger weight the ride may carry (kg). Beyond this the
    /// ride is overloaded and unsafe to start; the load must never exceed this.
    /// </summary>
    public const double MaxPassengerLoadKg = 3200d;

    // --- Natural passenger behaviour ---
    /// <summary>Earliest a seated passenger pulls their restraint down.</summary>
    public static readonly TimeSpan MinRestraintCloseDelay = TimeSpan.FromSeconds(10);

    /// <summary>Latest a seated passenger pulls their restraint down.</summary>
    public static readonly TimeSpan MaxRestraintCloseDelay = TimeSpan.FromSeconds(30);

    // --- Ride layout ---
    public const int HubCount = 4;
    public const int GondolasPerHub = 4;
    public const int SeatsPerGondola = 2;
}
