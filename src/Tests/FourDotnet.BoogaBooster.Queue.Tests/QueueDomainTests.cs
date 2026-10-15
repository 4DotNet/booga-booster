using FourDotnet.BoogaBooster.Core;
using FourDotnet.BoogaBooster.Queue.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the validation invariants of the Queue domain value objects and
/// aggregate constructors (ADR-0003).
/// </summary>
public class QueueDomainTests
{
    [Fact]
    public void Person_WithNonPositiveNumber_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Person(0, "Alice", 80, 75, 75, 0));
    }

    [Fact]
    public void Person_WithBlankName_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "  ", 80, 75, 75, 0));
    }

    [Theory]
    [InlineData(Person.MinWeightInKilograms - 1)]
    [InlineData(Person.MaxWeightInKilograms + 1)]
    public void Person_WithWeightOutOfRange_Throws(int weight)
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", weight, 75, 75, 0));
    }

    [Fact]
    public void Person_WithValidValues_HasIdentityAndStartsNew()
    {
        var person = new Person(42, "Alice", 80, 70, 90, 5);

        Assert.Equal(42, person.Number);
        Assert.Equal("Alice", person.Name);
        Assert.Equal(80, person.WeightInKilograms);
        Assert.Equal(70, person.Happiness);
        Assert.Equal(90, person.PreferredRideIntensity);
        Assert.Equal(5, person.Nausea);
        Assert.Equal(DomainModelState.New, person.State);
    }

    [Theory]
    [InlineData(Person.MinExperienceRating)]
    [InlineData(Person.MaxExperienceRating)]
    public void Person_WithExperienceOnTheBoundaries_IsAccepted(double rating)
    {
        var person = new Person(1, "Alice", 80, rating, rating, rating);

        Assert.Equal(rating, person.Happiness);
        Assert.Equal(rating, person.PreferredRideIntensity);
        Assert.Equal(rating, person.Nausea);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Person_WithHappinessOutOfRange_Throws(double happiness)
    {
        var exception = Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, happiness, 75, 0));

        Assert.Contains("happiness", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public void Person_WithPreferredRideIntensityOutOfRange_Throws(double preference)
    {
        var exception = Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, 75, preference, 0));

        Assert.Contains("preferred ride intensity", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(100.1)]
    [InlineData(double.NaN)]
    public void Person_WithNauseaOutOfRange_Throws(double nausea)
    {
        var exception = Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, 75, 75, nausea));

        Assert.Contains("nausea", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupArrival_WithEmptyGroupId_Throws()
    {
        var members = new[] { QueueTestData.Person() };

        Assert.Throws<DomainValidationException>(() => new GroupArrival(Guid.Empty, members));
    }

    [Fact]
    public void GroupArrival_WithNoMembers_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new GroupArrival(Guid.NewGuid(), []));
    }

    [Fact]
    public void GroupArrival_WithNullMembers_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new GroupArrival(Guid.NewGuid(), null!));
    }

    [Fact]
    public void GroupArrival_WithNullPerson_Throws()
    {
        var members = new Person[] { QueueTestData.Person(), null! };

        Assert.Throws<DomainValidationException>(() => new GroupArrival(Guid.NewGuid(), members));
    }

    [Fact]
    public void PersonGenerator_CreateGroup_BelowOne_Throws()
    {
        Assert.Throws<DomainValidationException>(() => QueueTestData.Generator().CreateGroup(0));
    }

    [Fact]
    public void PersonGenerator_CreateGroup_CreatesDistinctPeopleUnderOneGroup()
    {
        var arrival = QueueTestData.Generator().CreateGroup(3);

        Assert.Equal(3, arrival.Size);
        Assert.NotEqual(Guid.Empty, arrival.GroupId);
        Assert.Equal(3, arrival.Members.Select(m => m.Number).Distinct().Count());
        Assert.All(arrival.Members, m => Assert.False(string.IsNullOrWhiteSpace(m.Name)));
        Assert.All(
            arrival.Members,
            m => Assert.InRange(m.WeightInKilograms, Person.MinWeightInKilograms, Person.MaxWeightInKilograms));
    }

    [Fact]
    public void PersonGenerator_Next_DrawsStartingExperienceWithinTheArrivalRanges()
    {
        var generator = QueueTestData.Generator();

        var people = Enumerable.Range(0, 1_000).Select(_ => generator.Next()).ToArray();

        Assert.All(people, p => Assert.InRange(p.Happiness, 65d, 85d));
        Assert.All(people, p => Assert.InRange(p.PreferredRideIntensity, 50d, 100d));
        Assert.All(people, p => Assert.Equal(0d, p.Nausea));

        // A uniform draw over a thousand guests spreads across the band rather than
        // collapsing onto one value.
        Assert.True(people.Max(p => p.Happiness) - people.Min(p => p.Happiness) > 15d);
        Assert.True(people.Max(p => p.PreferredRideIntensity) - people.Min(p => p.PreferredRideIntensity) > 40d);
    }

    [Fact]
    public void PersonGenerator_WithTheSameSeed_ReproducesTheSameExperience()
    {
        var first = QueueTestData.Generator(seed: 2026);
        var second = QueueTestData.Generator(seed: 2026);

        var a = Enumerable.Range(0, 50).Select(_ => first.Next()).ToArray();
        var b = Enumerable.Range(0, 50).Select(_ => second.Next()).ToArray();

        Assert.Equal(a.Select(p => p.Happiness), b.Select(p => p.Happiness));
        Assert.Equal(a.Select(p => p.PreferredRideIntensity), b.Select(p => p.PreferredRideIntensity));
        Assert.Equal(a.Select(p => p.WeightInKilograms), b.Select(p => p.WeightInKilograms));
    }

    [Fact]
    public void RideQueue_WithEmptyRideId_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new RideQueue(Guid.Empty, maxPeople: 10, maxBoardableGroupSize: 32));
    }

    [Fact]
    public void RideQueue_WithNonPositiveMax_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new RideQueue(Guid.NewGuid(), maxPeople: 0, maxBoardableGroupSize: 32));
    }

    [Fact]
    public void RideQueue_CanAccept_NonPositiveSize_IsFalse()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: 32);

        Assert.False(queue.CanAccept(0));
        Assert.False(queue.CanAccept(-3));
    }

    [Fact]
    public void RideQueue_Enqueue_NullArrival_Throws()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: 32);

        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!, QueueTestData.QueuedAt));
    }
}
