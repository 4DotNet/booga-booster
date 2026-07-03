namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// The state of a gondola's yaw brake. A gondola has no motor: when the brake is
/// <see cref="Released"/> it spins freely under the centrifugal field; when
/// <see cref="Engaged"/> its rotation is blocked and held still.
/// </summary>
public enum GondolaBrakeState
{
    /// <summary>The brake is engaged — the gondola is held and cannot rotate.</summary>
    Engaged,

    /// <summary>The brake is released — the gondola spins freely.</summary>
    Released
}
