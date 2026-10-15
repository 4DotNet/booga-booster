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
    // The loss coefficients are sized against the mill's very large rotating inertia
    // (~1.65e5 kg·m² empty, ~3e5 loaded) so a de-powered mill coasts to rest in a
    // believable time. The increase is biased toward Coulomb/viscous friction — the
    // terms that dominate the low-speed tail — because aerodynamic drag (∝ ω²) is
    // already the largest loss at cruise, so raising it much would drop the terminal
    // speed and felt G. See docs/03 §3.3 and openspec change increase-spin-drag.
    public const double MillStallTorque = 60_000d;
    public const double MillMaxPowerWatts = 90_000d;
    public const double MillCoulombFriction = 10_000d;
    public const double MillViscousFriction = 3_500d;
    public const double MillAeroDrag = 6_000d;
    public const double MillMaxAngularVelocity = 2.5d;

    /// <summary>
    /// Braking torque added to the mill's Coulomb drag while the engine brake is
    /// engaged (N·m). A near-constant opposing torque, sized well above
    /// <see cref="MillStallTorque"/> against the mill's large inertia so a spinning
    /// mill (up to <see cref="MillMaxAngularVelocity"/>) is brought to a complete
    /// rest within a couple of seconds — a real brake, not just cutting power.
    /// </summary>
    public const double MillBrakeTorque = 200_000d;

    // --- Hub motor & losses ---
    // Raised alongside the mill (smaller factor) so the hubs also settle promptly and
    // no hub keeps creeping after the mill has effectively stopped.
    public const double HubStallTorque = 8_000d;
    public const double HubMaxPowerWatts = 15_000d;
    public const double HubCoulombFriction = 400d;
    public const double HubViscousFriction = 500d;
    public const double HubAeroDrag = 200d;
    public const double HubMaxAngularVelocity = 5d;

    /// <summary>
    /// Braking torque added to each hub's Coulomb drag while the engine brake is
    /// engaged (N·m). Sized above <see cref="HubStallTorque"/> for the hub inertia
    /// so a spinning hub (up to <see cref="HubMaxAngularVelocity"/>) settles to a
    /// complete rest within a couple of seconds, in step with the braking mill.
    /// </summary>
    public const double HubBrakeTorque = 20_000d;

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
    public static readonly TimeSpan MinRestraintCloseDelay = TimeSpan.FromSeconds(5);

    /// <summary>Latest a seated passenger pulls their restraint down.</summary>
    public static readonly TimeSpan MaxRestraintCloseDelay = TimeSpan.FromSeconds(10);

    // --- Passenger experience (docs/06-passenger-experience.md) ---
    /// <summary>The lowest value of an experience attribute (very sad, very tame, not nauseous).</summary>
    public const double ExperienceScaleMin = 0d;

    /// <summary>
    /// The highest value of an experience attribute (extremely happy, maximum allowed G,
    /// extremely nauseous) — and the ride intensity of a gondola at <see cref="MaxGForce"/>.
    /// </summary>
    public const double ExperienceScaleMax = 100d;

    /// <summary>
    /// Happiness of a passenger boarded straight into a seat, without a queue history:
    /// the midpoint of the queue's 65–85 arrival band (docs/06 §6.1).
    /// </summary>
    public const double DefaultRiderHappiness = 75d;

    /// <summary>
    /// Preferred ride intensity of a passenger boarded straight into a seat: the
    /// midpoint of the queue's 50–100 arrival band (docs/06 §6.1).
    /// </summary>
    public const double DefaultRiderPreferredIntensity = 75d;

    /// <summary>
    /// How close (in intensity points) the ride must be to a passenger's preference
    /// for their happiness to rise (docs/06 §6.4).
    /// </summary>
    public const double HappinessMatchBand = 15d;

    /// <summary>Happiness gained per second at an exact intensity match (points/s, docs/06 §6.4).</summary>
    public const double HappinessGainRate = 2d;

    /// <summary>
    /// How far (in intensity points) the ride must exceed a passenger's preference before
    /// their nausea starts to grow (docs/06 §6.5).
    /// </summary>
    public const double NauseaIntensityExcess = 30d;

    /// <summary>Exponential nausea growth rate while the ride is too intense (1/s, docs/06 §6.5).</summary>
    public const double NauseaGrowthRate = 0.15d;

    /// <summary>
    /// Offset that lets exponential nausea growth start from zero:
    /// <c>dN/dt = k (N + seed)</c> (points, docs/06 §6.5).
    /// </summary>
    public const double NauseaSeed = 1d;

    /// <summary>
    /// How long a gondola must stay at or above <see cref="MaxGForce"/> before its
    /// passengers take the over-G nausea penalty (docs/06 §6.6).
    /// </summary>
    public static readonly TimeSpan OverGNauseaDwell = TimeSpan.FromSeconds(1);

    /// <summary>Nausea points each passenger gains once per over-G episode (docs/06 §6.6).</summary>
    public const double OverGNauseaPenalty = 25d;

    // --- Ride layout ---
    public const int HubCount = 4;
    public const int GondolasPerHub = 4;
    public const int SeatsPerGondola = 2;
}
