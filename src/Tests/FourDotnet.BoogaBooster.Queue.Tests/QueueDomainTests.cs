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
        var person = new Person(42, "Alice", 80, 70, 90, 0);

        Assert.Equal(42, person.Number);
        Assert.Equal("Alice", person.Name);
        Assert.Equal(80, person.WeightInKilograms);
        Assert.Equal(70, person.ArrivalHappiness);
        Assert.Equal(90, person.PreferredRideIntensity);
        Assert.Equal(0, person.Nausea);
        Assert.Equal(DomainModelState.New, person.State);
    }

    [Theory]
    [InlineData(101, 75, 0)]
    [InlineData(-1, 75, 0)]
    [InlineData(75, 100.5, 0)]
    [InlineData(75, -0.1, 0)]
    [InlineData(75, 75, 100.01)]
    [InlineData(75, 75, -5)]
    [InlineData(double.NaN, 75, 0)]
    public void Person_WithMoodScoreOutOfRange_Throws(double happiness, double intensity, double nausea)
    {
        Assert.Throws<DomainValidationException>(
            () => new Person(1, "Alice", 80, happiness, intensity, nausea));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 100, 100)]
    public void Person_WithMoodScoresAtTheBounds_IsAccepted(double happiness, double intensity, double nausea)
    {
        var person = new Person(1, "Alice", 80, happiness, intensity, nausea);

        Assert.Equal(happiness, person.ArrivalHappiness);
        Assert.Equal(intensity, person.PreferredRideIntensity);
        Assert.Equal(nausea, person.Nausea);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(299)] // 4 min 59 s
    [InlineData(300)] // exactly the patience threshold
    [InlineData(-60)] // a clock that moved backwards
    public void HappinessAfterWaiting_WithinPatience_IsUnchanged(int waitSeconds)
    {
        var person = QueueTestData.Person(arrivalHappiness: 80);

        Assert.Equal(80, person.HappinessAfterWaiting(TimeSpan.FromSeconds(waitSeconds)));
    }

    [Fact]
    public void HappinessAfterWaiting_FifteenMinutes_IsHalved()
    {
        var person = QueueTestData.Person(arrivalHappiness: 80);

        Assert.Equal(40, person.HappinessAfterWaiting(TimeSpan.FromMinutes(15)), precision: 10);
    }

    [Fact]
    public void HappinessAfterWaiting_LongerWaits_AreStrictlyLower()
    {
        var person = QueueTestData.Person(arrivalHappiness: 80);

        var after10 = person.HappinessAfterWaiting(TimeSpan.FromMinutes(10));
        var after20 = person.HappinessAfterWaiting(TimeSpan.FromMinutes(20));
        var after30 = person.HappinessAfterWaiting(TimeSpan.FromMinutes(30));

        Assert.True(after10 < 80);
        Assert.True(after20 < after10);
        Assert.True(after30 < after20);
        Assert.True(after30 > 0);
        Assert.Equal(56.5685, after10, precision: 4);
        Assert.Equal(14.1421, after30, precision: 4);
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
    public void PersonGenerator_Next_DrawsInitialMoodWithinTheArrivalRanges()
    {
        var generator = QueueTestData.Generator();

        var people = Enumerable.Range(0, 1_000).Select(_ => generator.Next()).ToArray();

        Assert.All(people, p => Assert.InRange(p.ArrivalHappiness, 65, 85));
        Assert.All(people, p => Assert.InRange(p.PreferredRideIntensity, 50, 100));
        Assert.All(people, p => Assert.Equal(0, p.Nausea));

        // The draws are uniform over the whole range, not stuck on one value.
        Assert.True(people.Select(p => p.ArrivalHappiness).Distinct().Count() > 10);
        Assert.True(people.Select(p => p.PreferredRideIntensity).Distinct().Count() > 25);
    }

    [Fact]
    public void PersonGenerator_Next_WithTheSameSeed_ReproducesTheMood()
    {
        var first = QueueTestData.Generator(seed: 7);
        var second = QueueTestData.Generator(seed: 7);

        var a = Enumerable.Range(0, 10).Select(_ => first.Next()).ToArray();
        var b = Enumerable.Range(0, 10).Select(_ => second.Next()).ToArray();

        Assert.Equal(a.Select(p => p.ArrivalHappiness), b.Select(p => p.ArrivalHappiness));
        Assert.Equal(a.Select(p => p.PreferredRideIntensity), b.Select(p => p.PreferredRideIntensity));
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
