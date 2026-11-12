using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;

namespace FourDotnet.BoogaBooster.Queue.Abstractions;

/// <summary>
/// Public contract of the Queue module. Adds arriving groups to a ride's waiting
/// line and individuals to its single-riders line, reports the current state of
/// both, and lets a boarding caller take from either. Enqueuing a group publishes a
/// <c>GroupQueuedIntegrationEvent</c> as a side effect; enqueuing a single rider
/// publishes a <c>SingleRiderQueuedIntegrationEvent</c>.
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
    /// Returns a snapshot of the given ride's waiting lines: the groups in the regular
    /// queue and the individuals in the single-riders line. A ride nobody has joined
    /// reports both lines empty.
    /// </summary>
    GetQueueStatusResponse GetStatus(Guid rideId);

    /// <summary>
    /// Adds one freshly generated guest to the back of the given ride's single-riders
    /// line — a guest who agreed to be seated next to a stranger, as opposed to a
    /// group of one in the regular queue — publishes a single-rider-queued
    /// integration event, and returns the person as enqueued.
    /// </summary>
    /// <exception cref="FourDotnet.BoogaBooster.Core.DomainValidationException">
    /// The single-riders line is at its maximum length. Nobody already waiting is
    /// displaced and no event is published.
    /// </exception>
    Task<PersonDto> EnqueueSingleRiderAsync(Guid rideId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes up to <paramref name="count"/> individuals from the <em>front</em> of
    /// the given ride's single-riders line, in one atomic step, and returns them in
    /// arrival order — the guest who waited longest first.
    /// </summary>
    /// <remarks>
    /// The take is "up to <paramref name="count"/>, possibly empty, never throws" for
    /// any line state: when fewer individuals are waiting than requested, everyone
    /// waiting is returned and the line is left empty; when nobody is waiting, the
    /// ride has no single-riders line yet, or <paramref name="count"/> is zero or
    /// negative, an empty list is returned and the line is unchanged. A caller that
    /// cannot seat everyone it took must hand them back with
    /// <see cref="RequeueSingleRidersAsync"/>.
    /// </remarks>
    /// <returns>The removed individuals, in arrival order; never <c>null</c>.</returns>
    Task<IReadOnlyList<PersonDto>> TakeSingleRidersAsync(Guid rideId, int count, CancellationToken cancellationToken);

    /// <summary>
    /// Returns individuals that were taken with <see cref="TakeSingleRidersAsync"/>
    /// but could not be seated to the <em>front</em> of the given ride's
    /// single-riders line, ahead of everyone still waiting and in the relative order
    /// given, so a failed boarding neither loses a guest nor costs them their place.
    /// </summary>
    /// <remarks>
    /// Returning guests are re-admitted even when the line has meanwhile filled to
    /// its maximum: they already held their places. No integration event is
    /// published — they are not new arrivals. An empty list is a no-op.
    /// </remarks>
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
