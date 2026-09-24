using FourDotnet.BoogaBooster.Queue.Domain;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers how the <see cref="RideQueue"/> aggregate and its <see cref="QueuedGroup"/>s
/// turn a wait into the happiness they report: the join moment is stamped on enqueue,
/// the current value is derived from it under a <see cref="FakeTimeProvider"/>, the
/// line's average is the mean over people (or null when empty), and the stored
/// <see cref="Person.Profile"/> is never touched.
/// </summary>
public sealed class QueueHappinessTests
{
    private const int Precision = 10;

    [Fact]
    public void Enqueue_StampsQueuedAt_WithTheMomentSupplied()
    {
        var queue = QueueTestData.Queue();

        var group = queue.Enqueue(QueueTestData.Group(2), QueueTestData.Now);

        Assert.Equal(QueueTestData.Now, group.QueuedAt);
    }

    [Fact]
    public void EnqueueAll_StampsEveryGroupInTheBatch_WithTheSameMoment()
    {
        var queue = QueueTestData.Queue();

        var groups = queue.EnqueueAll([QueueTestData.Group(2), QueueTestData.Group(3)], QueueTestData.Now);

        Assert.All(groups, group => Assert.Equal(QueueTestData.Now, group.QueuedAt));
    }

    [Fact]
    public void WaitedFor_IsTheTimeSinceQueuedAt()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var group = QueueTestData.Queue().Enqueue(QueueTestData.Group(1), time.GetUtcNow());

        time.Advance(TimeSpan.FromMinutes(7));

        Assert.Equal(TimeSpan.FromMinutes(7), group.WaitedFor(time.GetUtcNow()));
    }

    [Fact]
    public void WaitedFor_BeforeQueuedAt_IsZeroNotNegative()
    {
        var group = QueueTestData.Queue().Enqueue(QueueTestData.Group(1), QueueTestData.Now);

        Assert.Equal(TimeSpan.Zero, group.WaitedFor(QueueTestData.Now - TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void CurrentHappiness_BeforeTheOnset_IsTheArrivalValue()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var group = QueueTestData.Queue().Enqueue(QueueTestData.GroupWithHappiness(0.8), time.GetUtcNow());

        time.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(0.8, group.CurrentHappiness(group.Members[0], time.GetUtcNow()));
    }

    [Fact]
    public void CurrentHappiness_ExactlyAtTheOnset_IsTheArrivalValue()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var group = QueueTestData.Queue().Enqueue(QueueTestData.GroupWithHappiness(0.8), time.GetUtcNow());

        time.Advance(TimeSpan.FromMinutes(5));

        Assert.Equal(0.8, group.CurrentHappiness(group.Members[0], time.GetUtcNow()));
    }

    [Fact]
    public void CurrentHappiness_PastTheOnset_ReflectsTheWait()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var group = QueueTestData.Queue().Enqueue(QueueTestData.GroupWithHappiness(0.8), time.GetUtcNow());

        time.Advance(TimeSpan.FromMinutes(15));

        Assert.Equal(0.7, group.CurrentHappiness(group.Members[0], time.GetUtcNow()), Precision);
    }

    [Fact]
    public void CurrentHappiness_AfterAVeryLongWait_FloorsAtZero()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var group = QueueTestData.Queue().Enqueue(QueueTestData.GroupWithHappiness(0.65), time.GetUtcNow());

        time.Advance(TimeSpan.FromHours(3));

        Assert.Equal(0, group.CurrentHappiness(group.Members[0], time.GetUtcNow()));
    }

    [Fact]
    public void CurrentHappiness_DoesNotRewriteTheStoredProfile()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var group = QueueTestData.Queue().Enqueue(QueueTestData.GroupWithHappiness(0.8), time.GetUtcNow());
        var member = group.Members[0];
        var storedProfile = member.Profile;

        time.Advance(TimeSpan.FromMinutes(15));
        var current = group.CurrentHappiness(member, time.GetUtcNow());

        Assert.NotEqual(storedProfile.Happiness, current);
        Assert.Equal(0.8, member.Profile.Happiness);
        Assert.Same(storedProfile, member.Profile);
    }

    [Fact]
    public void CurrentHappiness_UsesThePolicyTheQueueWasBuiltWith()
    {
        var policy = new GrumpinessPolicy(TimeSpan.FromMinutes(1), ratePerMinute: 0.1);
        var time = new FakeTimeProvider(QueueTestData.Now);
        var group = QueueTestData.Queue(policy: policy).Enqueue(QueueTestData.GroupWithHappiness(0.8), time.GetUtcNow());

        time.Advance(TimeSpan.FromMinutes(3));

        // Two minutes past a one-minute onset at 0.1 per minute.
        Assert.Equal(0.6, group.CurrentHappiness(group.Members[0], time.GetUtcNow()), Precision);
    }

    [Fact]
    public void CurrentHappiness_ForSomeoneNotInTheGroup_Throws()
    {
        var queue = QueueTestData.Queue();
        var group = queue.Enqueue(QueueTestData.Group(1), QueueTestData.Now);
        var stranger = QueueTestData.Person(number: 99);

        Assert.Throws<ArgumentException>(() => group.CurrentHappiness(stranger, QueueTestData.Now));
        Assert.Throws<ArgumentNullException>(() => group.CurrentHappiness(null!, QueueTestData.Now));
    }

    [Fact]
    public void GroupAverageHappiness_IsTheMeanOverItsMembers()
    {
        var group = QueueTestData.Queue().Enqueue(QueueTestData.GroupWithHappiness(0.6, 0.8), QueueTestData.Now);

        Assert.Equal(0.7, group.AverageHappiness(QueueTestData.Now), Precision);
    }

    [Fact]
    public void GroupAverageHappiness_ReflectsTheWait()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var group = QueueTestData.Queue().Enqueue(QueueTestData.GroupWithHappiness(0.6, 0.8), time.GetUtcNow());

        time.Advance(TimeSpan.FromMinutes(15));

        Assert.Equal(0.6, group.AverageHappiness(time.GetUtcNow()), Precision);
    }

    [Fact]
    public void QueueAverageHappiness_WhenEmpty_IsNull()
    {
        Assert.Null(QueueTestData.Queue().AverageHappiness(QueueTestData.Now));
    }

    [Fact]
    public void QueueAverageHappiness_IsTheMeanOverPeople_NotOverGroups()
    {
        var queue = QueueTestData.Queue();
        queue.Enqueue(QueueTestData.GroupWithHappiness(0.6), QueueTestData.Now);
        queue.Enqueue(QueueTestData.GroupWithHappiness(0.8, 0.8, 0.8), QueueTestData.Now);

        // (0.6 + 3 * 0.8) / 4, not (0.6 + 0.8) / 2.
        Assert.Equal(0.75, queue.AverageHappiness(QueueTestData.Now)!.Value, Precision);
    }

    [Fact]
    public void QueueAverageHappiness_WeighsEachGroupsOwnWait()
    {
        var time = new FakeTimeProvider(QueueTestData.Now);
        var queue = QueueTestData.Queue();
        queue.Enqueue(QueueTestData.GroupWithHappiness(0.8), time.GetUtcNow());
        time.Advance(TimeSpan.FromMinutes(10));
        queue.Enqueue(QueueTestData.GroupWithHappiness(0.8), time.GetUtcNow());
        time.Advance(TimeSpan.FromMinutes(5));

        // The first group has waited 15 minutes (0.7); the second exactly the onset (0.8).
        Assert.Equal(0.75, queue.AverageHappiness(time.GetUtcNow())!.Value, Precision);
    }

    [Fact]
    public void QueueAverageHappiness_AfterEveryoneLeaves_IsNullAgain()
    {
        var queue = QueueTestData.Queue();
        var group = queue.Enqueue(QueueTestData.GroupWithHappiness(0.6, 0.8), QueueTestData.Now);

        queue.Remove(group.GroupId);

        Assert.Null(queue.AverageHappiness(QueueTestData.Now));
    }

    [Fact]
    public void SnapshotAverageHappiness_DescribesTheSnapshot_NotTheLiveLine()
    {
        var queue = QueueTestData.Queue();
        var first = queue.Enqueue(QueueTestData.GroupWithHappiness(0.6), QueueTestData.Now);
        queue.Enqueue(QueueTestData.GroupWithHappiness(0.8, 0.8, 0.8), QueueTestData.Now);
        var snapshot = queue.SnapshotGroups();

        // The line moves on after the snapshot was taken: the first group boards.
        queue.Remove(first.GroupId);

        // The snapshot's average still covers the people it holds — (0.6 + 3 × 0.8) / 4 —
        // while the live line now averages 0.8.
        Assert.Equal(0.75, RideQueue.AverageHappiness(snapshot, QueueTestData.Now)!.Value, Precision);
        Assert.Equal(0.8, queue.AverageHappiness(QueueTestData.Now)!.Value, Precision);
    }

    [Fact]
    public void SnapshotAverageHappiness_OfAnEmptySnapshot_IsNull()
    {
        var snapshot = QueueTestData.Queue().SnapshotGroups();

        Assert.Null(RideQueue.AverageHappiness(snapshot, QueueTestData.Now));
    }

    [Fact]
    public void Store_BuildsEveryQueuesPolicy_FromTheOptions()
    {
        var options = Options.Create(new QueueModuleOptions
        {
            GrumpinessOnset = TimeSpan.FromMinutes(2),
            GrumpinessRatePerMinute = 0.05,
        });
        var store = new InMemoryRideQueueStore(options);

        var queue = store.GetOrCreate(Guid.NewGuid());

        Assert.Equal(new GrumpinessPolicy(TimeSpan.FromMinutes(2), 0.05), queue.GrumpinessPolicy);
    }
}
