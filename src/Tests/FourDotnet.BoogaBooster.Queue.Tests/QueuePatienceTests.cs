using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the queue-patience policy (docs/06-passenger-experience.md §6.2): waiting
/// is free for five minutes, then happiness decays exponentially with the extra wait.
/// </summary>
public sealed class QueuePatienceTests
{
    private const double ArrivalHappiness = 80d;

    [Fact]
    public void Policy_UsesTheDocumentedGraceAndTimeConstant()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), QueuePatience.GracePeriod);
        Assert.Equal(TimeSpan.FromMinutes(10), QueuePatience.DecayTimeConstant);
    }

    [Fact]
    public void Happiness_IsUnchanged_JustInsideTheGracePeriod()
    {
        var waited = TimeSpan.FromMinutes(4) + TimeSpan.FromSeconds(59);

        Assert.Equal(ArrivalHappiness, QueuePatience.HappinessAfter(ArrivalHappiness, waited));
    }

    [Fact]
    public void Happiness_IsUnchanged_ExactlyAtTheEndOfTheGracePeriod()
    {
        Assert.Equal(ArrivalHappiness, QueuePatience.HappinessAfter(ArrivalHappiness, QueuePatience.GracePeriod));
    }

    [Fact]
    public void Happiness_IsUnchanged_ForANegativeWait()
    {
        Assert.Equal(ArrivalHappiness, QueuePatience.HappinessAfter(ArrivalHappiness, TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public void Erosion_IsExponential_EqualExtraWaitsGiveEqualRatios()
    {
        var at5 = QueuePatience.HappinessAfter(ArrivalHappiness, TimeSpan.FromMinutes(5));
        var at8 = QueuePatience.HappinessAfter(ArrivalHappiness, TimeSpan.FromMinutes(8));
        var at11 = QueuePatience.HappinessAfter(ArrivalHappiness, TimeSpan.FromMinutes(11));

        Assert.True(at8 < at5, $"Happiness at 8 min ({at8}) should be below 5 min ({at5}).");
        Assert.Equal(at8 / at5, at11 / at8, precision: 12);

        // Three extra minutes over a ten-minute time constant: a factor of e^(−0.3).
        Assert.Equal(Math.Exp(-0.3d), at8 / at5, precision: 12);
    }

    [Fact]
    public void Erosion_MatchesTheClosedForm_AfterFifteenMinutes()
    {
        // 10 minutes past the grace period is exactly one time constant: 1/e left.
        var happiness = QueuePatience.HappinessAfter(ArrivalHappiness, TimeSpan.FromMinutes(15));

        Assert.Equal(ArrivalHappiness / Math.E, happiness, precision: 10);
    }

    [Fact]
    public void Erosion_ClampsAtZero_AndNeverGoesNegative()
    {
        var happiness = QueuePatience.HappinessAfter(ArrivalHappiness, TimeSpan.FromDays(365));

        Assert.Equal(Person.MinRating, happiness);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(101d)]
    [InlineData(double.NaN)]
    public void ArrivalHappiness_OutsideTheRatingRange_IsRejected(double arrivalHappiness)
    {
        Assert.Throws<DomainValidationException>(
            () => QueuePatience.HappinessAfter(arrivalHappiness, TimeSpan.FromMinutes(10)));
    }
}
