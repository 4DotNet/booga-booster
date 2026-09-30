namespace FourDotnet.BoogaBooster.DigitalTwin.Observability;

/// <summary>
/// The span attribute names the DigitalTwin module records, and the bounded values
/// its drive and transition-outcome metric tags take. Declared as constants in one
/// place so no call site repeats a literal, a rename is a single edit, and the tests
/// assert against the same name the production code writes.
/// </summary>
/// <remarks>
/// Hub index, gondola index and seat are span attributes only. As metric tags they
/// would multiply every series sixteen- or thirty-two-fold for no diagnostic gain,
/// while a span is already one record per operation (design D6).
/// </remarks>
internal static class RideTelemetryAttributes
{
    /// <summary>The ride a background pass addressed.</summary>
    internal const string RideId = "ride.id";

    /// <summary>The lifecycle state the operation found, or left the ride in.</summary>
    internal const string State = "ride.state";

    /// <summary>The lifecycle state a transition asked for.</summary>
    internal const string StateRequested = "ride.state.requested";

    /// <summary>The hub (0–3) the command addressed.</summary>
    internal const string HubIndex = "ride.hub.index";

    /// <summary>The gondola (0–3 on its hub) the command addressed.</summary>
    internal const string GondolaIndex = "ride.gondola.index";

    /// <summary>Which of the gondola's two seats the command addressed.</summary>
    internal const string Seat = "ride.seat";

    /// <summary>
    /// Which drive was commanded — <see cref="MainEngine"/> or <see cref="HubEngines"/>.
    /// This is what distinguishes an otherwise identical main- and hub-engine span.
    /// Doubles as a bounded metric tag key.
    /// </summary>
    internal const string Engine = "ride.engine";

    /// <summary>The throttle setting a power command asked for, 0–100.</summary>
    internal const string EnginePowerPercent = "ride.engine.power_percent";

    /// <summary>The rotation direction a direction command asked for.</summary>
    internal const string EngineDirection = "ride.engine.direction";

    /// <summary>The engine-brake state asked for, or already in effect.</summary>
    internal const string BrakeEngaged = "ride.brake.engaged";

    /// <summary>The engine-brake state the command found, so a no-op reads as one.</summary>
    internal const string BrakeEngagedBefore = "ride.brake.engaged.before";

    /// <summary>The gondola yaw-brake state a command asked for.</summary>
    internal const string GondolaBrake = "ride.gondola.brake";

    /// <summary>How many passengers are seated across all sixteen gondolas.</summary>
    internal const string PassengersBoarded = "ride.passengers.boarded";

    /// <summary>How many groups a loading pass seated.</summary>
    internal const string GroupsBoarded = "ride.groups.boarded";

    /// <summary>The mill's signed rotation speed in revolutions per minute.</summary>
    internal const string MillRpm = "ride.mill.rpm";

    /// <summary>
    /// Whether a boarding command supplied a weight — never the weight itself, and
    /// never a passenger's name (ADR-0009 forbids personal data on a span).
    /// </summary>
    internal const string WeightSupplied = "ride.passenger.weight_supplied";

    /// <summary>The <see cref="Engine"/> value for the great mill's drive.</summary>
    internal const string MainEngine = "main";

    /// <summary>The <see cref="Engine"/> value for the four hub drives.</summary>
    internal const string HubEngines = "hub";
}
