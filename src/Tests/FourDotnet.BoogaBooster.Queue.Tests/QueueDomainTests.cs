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
        Assert.Throws<DomainValidationException>(() => new Person(0, "Alice", 80, QueueTestData.Profile()));
    }

    [Fact]
    public void Person_WithBlankName_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "  ", 80, QueueTestData.Profile()));
    }

    [Theory]
    [InlineData(Person.MinWeightInKilograms - 1)]
    [InlineData(Person.MaxWeightInKilograms + 1)]
    public void Person_WithWeightOutOfRange_Throws(int weight)
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", weight, QueueTestData.Profile()));
    }

    [Fact]
    public void Person_WithValidValues_HasIdentityAndStartsNew()
    {
        var person = new Person(42, "Alice", 80, QueueTestData.Profile());

        Assert.Equal(42, person.Number);
        Assert.Equal("Alice", person.Name);
        Assert.Equal(80, person.WeightInKilograms);
        Assert.Equal(DomainModelState.New, person.State);
    }

    [Fact]
    public void Person_WithValidProfile_ReportsExactlyThoseValues()
    {
        var person = new Person(42, "Alice", 80, new RiderProfile(0.6, 0.7, 0));

        Assert.Equal(0.6, person.Profile.PreferredIntensity);
        Assert.Equal(0.7, person.Profile.Happiness);
        Assert.Equal(0, person.Profile.Nausea);
    }

    [Fact]
    public void Person_WithNullProfile_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, null!));
    }

    [Fact]
    public void Person_WithInvalidProfile_IsNeverCreated()
    {
        // The spec phrases the bound violations as "a person is created with ...":
        // the value object rejects them before a person can exist.
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, new RiderProfile(0.05, 0.7, 0)));
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, new RiderProfile(0.6, 1.2, 0)));
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", 80, new RiderProfile(0.6, 0.7, double.NaN)));
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
    public void RideQueue_WithEmptyRideId_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new RideQueue(Guid.Empty, maxPeople: 10, maxBoardableGroupSize: 32, QueueTestData.DefaultPolicy));
    }

    [Fact]
    public void RideQueue_WithNonPositiveMax_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new RideQueue(Guid.NewGuid(), maxPeople: 0, maxBoardableGroupSize: 32, QueueTestData.DefaultPolicy));
    }

    [Fact]
    public void RideQueue_WithNullGrumpinessPolicy_Throws()
    {
        Assert.Throws<DomainValidationException>(
            () => new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: 32, null!));
    }

    [Fact]
    public void RideQueue_ExposesThePolicyItWasBuiltWith()
    {
        var policy = new GrumpinessPolicy(TimeSpan.FromMinutes(2), 0.05);

        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: 32, policy);

        Assert.Same(policy, queue.GrumpinessPolicy);
    }

    [Fact]
    public void RideQueue_CanAccept_NonPositiveSize_IsFalse()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: 32, QueueTestData.DefaultPolicy);

        Assert.False(queue.CanAccept(0));
        Assert.False(queue.CanAccept(-3));
    }

    [Fact]
    public void RideQueue_Enqueue_NullArrival_Throws()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10, maxBoardableGroupSize: 32, QueueTestData.DefaultPolicy);

        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!, QueueTestData.Now));
    }
}
