namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// The nested-frame kinematics (see <c>docs/01-physics-overview.md</c> and
/// <c>docs/05-forces-and-g-forces.md</c>): where a gondola's pivot sits in the
/// world given the mill and hub angles, and the horizontal centrifugal field it
/// feels there. Pure geometry — no state.
/// </summary>
internal static class RideKinematics
{
    private const double HalfPi = Math.PI / 2d;

    /// <summary>The fixed mount angle of arm <paramref name="index"/> (0–3): 0°, 90°, 180°, 270°.</summary>
    public static double MountAngle(int index) => index * HalfPi;

    /// <summary>World position of hub <paramref name="hubIndex"/>'s centre.</summary>
    public static PlanarVector HubCentre(double millAngle, int hubIndex)
        => PlanarVector.FromAngle(millAngle + MountAngle(hubIndex)) * RideParameters.MillArmLength;

    /// <summary>World position of gondola <paramref name="gondolaIndex"/>'s pivot on the given hub.</summary>
    public static PlanarVector PivotPosition(double millAngle, int hubIndex, double hubAngle, int gondolaIndex)
    {
        var hubCentre = HubCentre(millAngle, hubIndex);
        var armWorldAngle = millAngle + hubAngle + MountAngle(gondolaIndex);
        return hubCentre + (PlanarVector.FromAngle(armWorldAngle) * RideParameters.HubArmLength);
    }

    /// <summary>
    /// The outward horizontal acceleration field at a pivot — the vector sum of the
    /// mill and hub centripetal contributions, each pointing away from its own axis
    /// with magnitude <c>ω²·r</c>. This is what drives the passive swing and what a
    /// rider feels as horizontal specific force.
    /// </summary>
    public static PlanarVector OutwardField(
        PlanarVector pivot,
        PlanarVector hubCentre,
        double millOmega,
        double hubOmega)
    {
        var fromMill = pivot * (millOmega * millOmega);
        var fromHub = (pivot - hubCentre) * (hubOmega * hubOmega);
        return fromMill + fromHub;
    }
}
