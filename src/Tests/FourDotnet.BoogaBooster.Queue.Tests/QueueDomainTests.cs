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
        Assert.Throws<DomainValidationException>(() => new Person(0, "Alice", 80));
    }

    [Fact]
    public void Person_WithBlankName_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "  ", 80));
    }

    [Theory]
    [InlineData(Person.MinWeightInKilograms - 1)]
    [InlineData(Person.MaxWeightInKilograms + 1)]
    public void Person_WithWeightOutOfRange_Throws(int weight)
    {
        Assert.Throws<DomainValidationException>(() => new Person(1, "Alice", weight));
    }

    [Fact]
    public void Person_WithValidValues_HasIdentityAndStartsNew()
    {
        var person = new Person(42, "Alice", 80);

        Assert.Equal(42, person.Number);
        Assert.Equal("Alice", person.Name);
        Assert.Equal(80, person.WeightInKilograms);
        Assert.Equal(DomainModelState.New, person.State);
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
        Assert.Throws<DomainValidationException>(() => new RideQueue(Guid.Empty, maxPeople: 10));
    }

    [Fact]
    public void RideQueue_WithNonPositiveMax_Throws()
    {
        Assert.Throws<DomainValidationException>(() => new RideQueue(Guid.NewGuid(), maxPeople: 0));
    }

    [Fact]
    public void RideQueue_CanAccept_NonPositiveSize_IsFalse()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10);

        Assert.False(queue.CanAccept(0));
        Assert.False(queue.CanAccept(-3));
    }

    [Fact]
    public void RideQueue_Enqueue_NullArrival_Throws()
    {
        var queue = new RideQueue(Guid.NewGuid(), maxPeople: 10);

        Assert.Throws<ArgumentNullException>(() => queue.Enqueue(null!));
    }
}
