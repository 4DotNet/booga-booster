using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the three experience ratings a <see cref="Person"/> carries
/// (docs/06-passenger-experience.md §6.1): each is accepted anywhere in [0, 100],
/// inclusive, and any value outside that range — or not a number — is rejected.
/// </summary>
public sealed class PersonTests
{
    [Theory]
    [InlineData(Person.MinRating)]
    [InlineData(50d)]
    [InlineData(Person.MaxRating)]
    public void Ratings_AtOrWithinTheBounds_AreAccepted(double rating)
    {
        var person = new Person(1, "Alice", 80, rating, rating, rating);

        Assert.Equal(rating, person.Happiness);
        Assert.Equal(rating, person.PreferredIntensity);
        Assert.Equal(rating, person.Nausea);
    }

    [Theory]
    [InlineData(-0.001d)]
    [InlineData(100.001d)]
    [InlineData(120d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Happiness_OutsideTheBounds_IsRejected(double happiness)
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, happiness, 75d, 0d));
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(101d)]
    [InlineData(double.NaN)]
    public void PreferredIntensity_OutsideTheBounds_IsRejected(double preferredIntensity)
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, 75d, preferredIntensity, 0d));
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(101d)]
    [InlineData(double.NegativeInfinity)]
    public void Nausea_OutsideTheBounds_IsRejected(double nausea)
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, 75d, 75d, nausea));
    }

    [Fact]
    public void Rejection_NamesTheOffendingRating()
    {
        var exception = Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, 75d, 75d, 101d));

        Assert.Contains(nameof(Person.Nausea), exception.Message, StringComparison.Ordinal);
    }
}
