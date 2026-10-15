using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers ride intensity (docs/06 §6.3): felt G as a percentage of the maximum
/// allowed G-force, clamped to the 0–100 experience scale.
/// </summary>
public sealed class RideIntensityTests
{
    private const double Precision = 1e-9;

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2.25, 50)]  // half of max G
    [InlineData(4.5, 100)]  // max G
    [InlineData(5.0, 100)]  // beyond max G is still full intensity
    public void Intensity_is_felt_g_as_a_share_of_max_g(double feltG, double expected)
    {
        Assert.Equal(expected, RideIntensity.FromFeltG(feltG), Precision);
    }

    [Fact]
    public void Felt_g_is_the_magnitude_of_the_forward_and_lateral_components()
    {
        // A 3-4-5 triangle scaled to land exactly on half of max G.
        Assert.Equal(2.25d, RideIntensity.FeltG(forwardG: 1.35d, lateralG: -1.8d), Precision);
    }

    [Fact]
    public void A_gondola_at_rest_feels_no_intensity()
    {
        var gondola = new Gondola(0, 0);

        gondola.AdvancePhysics(0d, 0d, 0d, 0d, TestHelpers.Dt.TotalSeconds);

        Assert.Equal(0d, gondola.Intensity, Precision);
    }

    [Fact]
    public void A_spinning_gondola_reports_the_intensity_of_the_g_force_it_streams()
    {
        var gondola = new Gondola(0, 0);

        gondola.AdvancePhysics(millAngle: 0d, millOmega: 1.2d, hubAngle: 0d, hubOmega: 0.8d, dt: TestHelpers.Dt.TotalSeconds);

        var expectedFeltG = Math.Sqrt((gondola.ForwardG * gondola.ForwardG) + (gondola.LateralG * gondola.LateralG));
        Assert.True(expectedFeltG > 0.5d, "The spinning rig should produce a noticeable G-force.");
        Assert.Equal(expectedFeltG, gondola.FeltG, Precision);
        Assert.Equal(100d * expectedFeltG / RideParameters.MaxGForce, gondola.Intensity, Precision);
    }
}
