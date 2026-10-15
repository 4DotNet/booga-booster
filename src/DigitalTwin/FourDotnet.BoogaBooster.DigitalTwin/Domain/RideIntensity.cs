namespace FourDotnet.BoogaBooster.DigitalTwin.Domain;

/// <summary>
/// How intense the ride feels in a gondola: its felt G as a share of the maximum
/// allowed G-force, on the passenger-experience scale (docs/06 §6.3, design D5).
/// <code>
/// G_felt    = √(ForwardG² + LateralG²)
/// intensity = clamp(100 · G_felt / MaxGForce, 0, 100)
/// </code>
/// </summary>
public static class RideIntensity
{
    /// <summary>
    /// The felt G-force: the magnitude of the forward and lateral components a gondola
    /// computes and streams each tick.
    /// </summary>
    public static double FeltG(double forwardG, double lateralG) => double.Hypot(forwardG, lateralG);

    /// <summary>
    /// The ride intensity for a felt G-force: 50 at half of
    /// <see cref="RideParameters.MaxGForce"/>, 100 at or above it.
    /// </summary>
    public static double FromFeltG(double feltG) =>
        Math.Clamp(
            RideParameters.ExperienceScaleMax * feltG / RideParameters.MaxGForce,
            RideParameters.ExperienceScaleMin,
            RideParameters.ExperienceScaleMax);
}
