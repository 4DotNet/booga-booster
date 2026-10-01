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

    // --- Rider mood (docs/06-rider-mood.md) ---
    /// <summary>The lowest a mood value (happiness or nausea) may be.</summary>
    public const double MinMood = 0d;

    /// <summary>The highest a mood value (happiness or nausea) may be.</summary>
    public const double MaxMood = 100d;

    /// <summary>The lowest preferred G a rider may have: half the safe limit (g).</summary>
    public const double MinPreferredG = MaxGForce / 2d;

    /// <summary>The highest preferred G a rider may have: the safe limit itself (g).</summary>
    public const double MaxPreferredG = MaxGForce;

    /// <summary>
    /// Happiness of a rider boarded without a queue identity (a manual boarding):
    /// the middle of the queue's starting range of 65–85.
    /// </summary>
    public const double DefaultPassengerHappiness = 75d;

    /// <summary>Preferred G of a rider boarded without a queue identity: the middle of the preferred-G range (g).</summary>
    public const double DefaultPassengerPreferredG = (MinPreferredG + MaxPreferredG) / 2d;

    /// <summary>Happiness gained per second by a rider whose felt G exactly matches their preference.</summary>
    public const double HappinessGainPerSecond = 2d;

    /// <summary>How far (g) felt G may be from the preference and still be fun; the gain falls linearly to 0 at this distance.</summary>
    public const double FunBand = 1d;

    /// <summary>How far above their preference, as a fraction of <see cref="MaxGForce"/>, felt G must be before a rider gets nauseous.</summary>
    public const double NauseaToleranceFraction = 0.3d;

    /// <summary>Exponential growth rate of nausea while overshooting (1/s).</summary>
    public const double NauseaGrowthRate = 0.1d;

    /// <summary>Offset that lets nausea grow from 0: growth is <c>rate × (nausea + seed)</c> per second.</summary>
    public const double NauseaSeed = 5d;

    /// <summary>How long (s) a gondola must stay at or above <see cref="MaxGForce"/> before its riders take the penalty.</summary>
    public const double MaxGPenaltySeconds = 1d;

    /// <summary>Nausea added once per continuous stretch at or above <see cref="MaxGForce"/> longer than <see cref="MaxGPenaltySeconds"/>.</summary>
    public const double MaxGNauseaPenalty = 25d;

    // --- Ride layout ---
    public const int HubCount = 4;
    public const int GondolasPerHub = 4;
    public const int SeatsPerGondola = 2;
}
