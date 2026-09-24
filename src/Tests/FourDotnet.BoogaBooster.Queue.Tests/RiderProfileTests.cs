using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the <see cref="RiderProfile"/> value object (ADR-0003): every value is
/// range-checked and finite on construction, so a profile can never exist in an
/// invalid state.
/// </summary>
public sealed class RiderProfileTests
{
    [Fact]
    public void Constructor_WithValidValues_ReportsExactlyThoseValues()
    {
        var profile = new RiderProfile(0.6, 0.7, 0);

        Assert.Equal(0.6, profile.PreferredIntensity);
        Assert.Equal(0.7, profile.Happiness);
        Assert.Equal(0, profile.Nausea);
    }

    [Theory]
    [InlineData(RiderProfile.MinPreferredIntensity, RiderProfile.MinHappiness, RiderProfile.MinNausea)]
    [InlineData(RiderProfile.MaxPreferredIntensity, RiderProfile.MaxHappiness, RiderProfile.MaxNausea)]
    public void Constructor_AtTheBounds_IsAccepted(double preferredIntensity, double happiness, double nausea)
    {
        var profile = new RiderProfile(preferredIntensity, happiness, nausea);

        Assert.Equal(preferredIntensity, profile.PreferredIntensity);
        Assert.Equal(happiness, profile.Happiness);
        Assert.Equal(nausea, profile.Nausea);
    }

    [Theory]
    [InlineData(0.05)]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WithPreferredIntensityOutOfRangeOrNonFinite_Throws(double preferredIntensity)
    {
        var exception = Assert.Throws<DomainValidationException>(() => new RiderProfile(preferredIntensity, 0.7, 0));

        Assert.Contains("Preferred intensity", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1.2)]
    [InlineData(-0.01)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WithHappinessOutOfRangeOrNonFinite_Throws(double happiness)
    {
        var exception = Assert.Throws<DomainValidationException>(() => new RiderProfile(0.6, happiness, 0));

        Assert.Contains("Happiness", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(-0.5)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_WithNauseaOutOfRangeOrNonFinite_Throws(double nausea)
    {
        var exception = Assert.Throws<DomainValidationException>(() => new RiderProfile(0.6, 0.7, nausea));

        Assert.Contains("Nausea", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Profiles_WithTheSameValues_AreEqual()
    {
        // Value semantics: two profiles drawn identically are the same profile, which
        // is what the seeded-generator reproducibility test relies on.
        Assert.Equal(new RiderProfile(0.6, 0.7, 0), new RiderProfile(0.6, 0.7, 0));
        Assert.NotEqual(new RiderProfile(0.6, 0.7, 0), new RiderProfile(0.6, 0.7, 0.1));
    }
}
