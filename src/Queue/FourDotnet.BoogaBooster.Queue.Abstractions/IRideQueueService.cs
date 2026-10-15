using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

namespace FourDotnet.BoogaBooster.Queue.Abstractions;

/// <summary>
/// Public contract of the Queue module. Adds arriving groups to a ride's waiting
/// line and reports the current line state. Enqueuing a group publishes a
/// <c>GroupQueuedIntegrationEvent</c> as a side effect.
/// </summary>
public interface IRideQueueService
{
    /// <summary>
    /// Adds a group of <paramref name="groupSize"/> guests to the back of the
    /// given ride's queue, publishes a group-queued integration event for each group
    /// that joins the line, and returns the groups as enqueued.
    /// </summary>
    /// <remarks>
    /// An arrival larger than the ride's total seat capacity could never be seated in
    /// one boarding pass, so it is split into the smallest number of equally sized
    /// boardable groups, enqueued adjacently. Every other arrival joins the line as a
    /// single group, so the result usually holds exactly one entry.
    /// </remarks>
    /// <returns>The enqueued groups, in the order they now sit in the line.</returns>
    /// <exception cref="System.ArgumentOutOfRangeException">
    /// <paramref name="groupSize"/> is less than one.
    /// </exception>
    Task<IReadOnlyList<QueuedGroupDto>> EnqueueGroupAsync(Guid rideId, int groupSize, CancellationToken cancellationToken);

    /// <summary>
    /// Returns a snapshot of the given ride's waiting lines: the groups and the
    /// single-riders line, taken together so they describe the same instant.
    /// </summary>
    GetQueueStatusResponse GetStatus(Guid rideId);

    /// <summary>
    /// Generates one individual guest, appends them to the back of the given ride's
    /// single-riders line and publishes a single-rider-queued integration event.
    /// </summary>
    /// <returns>The person as enqueued.</returns>
    /// <exception cref="FourDotnet.BoogaBooster.Core.DomainValidationException">
    /// The single-riders line is at its maximum length.
    /// </exception>
    Task<PersonDto> EnqueueSingleRiderAsync(Guid rideId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes up to <paramref name="count"/> individuals from the front of the given
    /// ride's single-riders line, in arrival order, in one atomic step.
    /// </summary>
    /// <returns>
    /// The individuals taken — fewer than <paramref name="count"/> when the line is
    /// shorter, and an empty list (never an exception) when nobody is waiting, the
    /// ride has no line yet, or <paramref name="count"/> is zero or less.
    /// </returns>
    Task<IReadOnlyList<PersonDto>> TakeSingleRidersAsync(Guid rideId, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Returns individuals previously taken with <see cref="TakeSingleRidersAsync"/>
    /// to the <b>front</b> of the ride's single-riders line, keeping their original
    /// relative order, so a boarding that failed costs nobody their place.
    /// </summary>
    Task RequeueSingleRidersAsync(Guid rideId, IReadOnlyList<PersonDto> people, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the group identified by <paramref name="groupId"/> from the given
    /// ride's queue — wherever it currently sits in the line — and returns it, so a
    /// boarding caller can take a specific group it chose from a
    /// <see cref="GetStatus"/> snapshot (including a group behind the front when the
    /// front group is too large to board). The order of the remaining groups is
    /// preserved.
    /// </summary>
    /// <returns>
    /// The removed group, or <c>null</c> when no such group is in the ride's queue
    /// — for example it was already taken or the ride has no queue. A caller should
    /// treat <c>null</c> as "already gone" and move on rather than retry.
    /// </returns>
    Task<QueuedGroupDto?> TakeGroupAsync(Guid rideId, Guid groupId, CancellationToken cancellationToken);
}
