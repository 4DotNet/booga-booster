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

    // --- Rider experience (docs/06-rider-experience.md; scores on a 0–100 scale) ---
    /// <summary>The lowest value of any mood score or ride intensity.</summary>
    public const double MinMoodScore = 0d;

    /// <summary>The highest value of any mood score or ride intensity (intensity is a percentage of <see cref="MaxGForce"/>).</summary>
    public const double MaxMoodScore = 100d;

    /// <summary>Happiness of a passenger boarded without a known mood — the midpoint of the 65–85 arrival range (docs/06 §6.1).</summary>
    public const double DefaultPassengerHappiness = 75d;

    /// <summary>Preferred ride intensity of a passenger boarded without a known mood — the midpoint of the 50–100 arrival range (docs/06 §6.1).</summary>
    public const double DefaultPreferredRideIntensity = 75d;

    /// <summary>Nausea of a passenger boarded without a known mood: nobody boards nauseous (docs/06 §6.1).</summary>
    public const double DefaultPassengerNausea = 0d;

    /// <summary>Happiness gained per second at an exact intensity match, <c>R_h</c> (docs/06 §6.4).</summary>
    public const double HappinessGainPerSecond = 1d;

    /// <summary>
    /// Half-width of the comfort band, <c>B</c>, in percentage points: the gain fades
    /// linearly to zero this far either side of the preferred intensity (docs/06 §6.4).
    /// </summary>
    public const double ComfortHalfBand = 15d;

    /// <summary>
    /// How far, in percentage points, the intensity must exceed a rider's preference
    /// before their nausea grows, <c>Δ_N</c> (docs/06 §6.5).
    /// </summary>
    public const double NauseaIntensityThreshold = 30d;

    /// <summary>Time for <c>N + N₀</c> to double while over-intense, <c>T_N</c>, in seconds (docs/06 §6.5).</summary>
    public const double NauseaDoublingSeconds = 10d;

    /// <summary>
    /// The seed <c>N₀</c> that lets exponential nausea leave zero: growth is
    /// proportional to <c>N + N₀</c> (docs/06 §6.5).
    /// </summary>
    public const double NauseaSeed = 1d;

    /// <summary>How long riders must feel at least <see cref="MaxGForce"/> without a break before an over-G shock, <c>T_shock</c> (docs/06 §6.6).</summary>
    public static readonly TimeSpan OverGShockHold = TimeSpan.FromSeconds(1);

    /// <summary>Nausea each occupant gains from one over-G shock, <c>N_shock</c> (docs/06 §6.6).</summary>
    public const double OverGShockNausea = 25d;

    // --- Ride layout ---
    public const int HubCount = 4;
    public const int GondolasPerHub = 4;
    public const int SeatsPerGondola = 2;
}
