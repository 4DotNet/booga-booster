namespace FourDotnet.BoogaBooster.DigitalTwin.Abstractions;

/// <summary>
/// Which of a gondola's two seats a passenger occupies. The two seats sit on
/// opposite sides of the gondola pivot, so a single-sided load shifts the centre
/// of mass sideways and makes the gondola swing asymmetrically.
/// </summary>
public enum SeatPosition
{
    /// <summary>The left-hand seat (positive lateral offset from the pivot).</summary>
    Left,

    /// <summary>The right-hand seat (negative lateral offset from the pivot).</summary>
    Right
}
