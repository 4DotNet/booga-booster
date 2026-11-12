using System.Diagnostics;
using FourDotnet.BoogaBooster.Core.Observability;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using FourDotnet.BoogaBooster.DigitalTwin.Observability;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using Microsoft.Extensions.Logging;

namespace FourDotnet.BoogaBooster.DigitalTwin.Application;

/// <summary>
/// Drains a ride's waiting line into its free seats while the ride is
/// <see cref="RideState.Loading"/>. Each pass repeatedly boards the first waiting
/// group that fits the remaining capacity — looking ahead past a too-large front
/// group to the next two groups to backfill smaller ones — and stops when no group
/// in that window fits (the ride is full for groups) or the queue empties. Only then
/// does the same pass top up every seat that is still free — including the seat
/// beside an existing rider — with individuals from the single-riders line, so groups
/// always come first and single riders only plug the gaps. Because a pass is
/// idempotent and re-runs every simulation tick, a group or single rider that joins
/// while the ride is still loading boards on the next tick.
/// </summary>
/// <remarks>
/// The Queue contract (<see cref="IRideQueueService"/>) is an optional dependency so
/// the DigitalTwin module still resolves on its own: when the Queue module is not
/// present, every loading pass is a no-op.
/// </remarks>
public sealed class RideLoadingCoordinator
{
    /// <summary>How far past the front of the line a pass looks to backfill a fitting group.</summary>
    private const int LookAheadWindow = 3;

    private const string LoadingPassOperationName = "RideLoadingPass";

    private readonly IRideStore _store;
    private readonly ILogger<RideLoadingCoordinator> _logger;
    private readonly IRideQueueService? _queueService;

    public RideLoadingCoordinator(
        IRideStore store,
        ILogger<RideLoadingCoordinator> logger,
        IRideQueueService? queueService = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _queueService = queueService;
    }

    /// <summary>
    /// Runs a single loading pass for <paramref name="rideId"/>. Does nothing unless
    /// the ride is <see cref="RideState.Loading"/> and the Queue module is wired up.
    /// </summary>
    /// <remarks>
    /// The pass instruments itself, so the 120 Hz caller needs neither a return value
    /// to inspect nor any knowledge of what a pass did (design D5). It spans only the
    /// passes worth a span — one that boarded somebody, or one that threw — because a
    /// pass runs every simulation tick and almost all of them do nothing (design D4).
    /// The activity is therefore started lazily and covers the rest of the pass.
    /// </remarks>
    public async Task RunLoadingPassAsync(Guid rideId, CancellationToken cancellationToken)
    {
        if (_queueService is null || _store.CurrentState != RideState.Loading)
        {
            return;
        }

        Activity? activity = null;
        var groupsBoarded = 0;
        var passengersBoarded = 0;
        var singleRidersSeated = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var emptyGondolas = _store.EmptyGondolaCount;
                if (emptyGondolas <= 0)
                {
                    break; // No empty gondola left to seat a fresh group — group loading is done.
                }

                var status = _queueService.GetStatus(rideId);
                if (status.Groups.Count == 0)
                {
                    break; // No group waiting.
                }

                var chosen = FirstFittingGroup(status.Groups, emptyGondolas);
                if (chosen is null)
                {
                    break; // None of the first three waiting groups fit — group loading is done.
                }

                var taken = await _queueService
                    .TakeGroupAsync(rideId, chosen.GroupId, cancellationToken)
                    .ConfigureAwait(false);
                if (taken is null)
                {
                    continue; // The group was already gone; re-read the line and retry.
                }

                var members = taken.People
                    .Select(person => new PassengerWeight(person.WeightInKilograms))
                    .ToArray();
                _store.BoardGroup(members);

                activity ??= StartPassActivity(rideId);
                groupsBoarded++;
                passengersBoarded += members.Length;

                BoogaBoosterTelemetry.PassengersBoarded.Add(members.Length);

                _logger.LogInformation(
                    "Boarded group {GroupId} of {Size} onto ride {RideId}; {EmptyGondolas} gondola(s) still free.",
                    taken.GroupId,
                    taken.Size,
                    rideId,
                    _store.EmptyGondolaCount);
            }

            // Phase two: group loading can place no further group, so single riders
            // top up whatever seats are still free — including the seat beside an
            // existing rider, which no group may take.
            singleRidersSeated = await TopUpWithSingleRidersAsync(_queueService, rideId, cancellationToken)
                .ConfigureAwait(false);

            if (singleRidersSeated > 0)
            {
                activity ??= StartPassActivity(rideId);
                passengersBoarded += singleRidersSeated;

                BoogaBoosterTelemetry.PassengersBoarded.Add(singleRidersSeated);

                _logger.LogInformation(
                    "Seated {Count} single rider(s) onto ride {RideId}; {FreeSeats} seat(s) still free.",
                    singleRidersSeated,
                    rideId,
                    _store.FreeSeatCount);
            }
        }
        catch (Exception exception)
        {
            // A pass that threw is worth a span even if it boarded nobody: the caller
            // logs and continues, so without this the broken pass leaves no trace.
            activity ??= StartPassActivity(rideId);
            activity?.AddException(exception);
            activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
            throw;
        }
        finally
        {
            if (activity is not null)
            {
                activity.SetTag(RideTelemetryAttributes.GroupsBoarded, groupsBoarded);
                activity.SetTag(RideTelemetryAttributes.PassengersBoarded, passengersBoarded);
                activity.SetTag(RideTelemetryAttributes.SingleRidersSeated, singleRidersSeated);
                activity.Dispose();
            }
        }
    }

    /// <summary>
    /// The top-up phase of a loading pass: seats waiting single riders into every
    /// seat that is still free once group loading has stopped, and returns how many
    /// were seated. A full ride or an empty single-riders line makes it a silent no-op.
    /// </summary>
    /// <remarks>
    /// The take is bulk and from the front of the line, so riders board in arrival
    /// order. A rider who was taken but not seated — the ride's capacity or state
    /// moved between the take and the seating, or the seating was rejected — goes
    /// back to the <em>front</em> of the line instead of being dropped. The store
    /// lock is the arbiter: seating is all-or-nothing, so "not seated" is either
    /// everyone or the tail the clamp cut off.
    /// </remarks>
    private async Task<int> TopUpWithSingleRidersAsync(
        IRideQueueService queueService,
        Guid rideId,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || _store.CurrentState != RideState.Loading)
        {
            return 0; // Single riders board only while the ride is still loading.
        }

        var freeSeats = _store.FreeSeatCount;
        if (freeSeats <= 0)
        {
            return 0; // Every seat is taken — nobody is even asked to step forward.
        }

        var taken = await queueService
            .TakeSingleRidersAsync(rideId, freeSeats, cancellationToken)
            .ConfigureAwait(false);
        if (taken.Count == 0)
        {
            return 0; // No single rider waiting.
        }

        var seated = 0;
        try
        {
            // Clamp to the live capacity immediately before seating: the riders have
            // already left the queue, so a seat or a state that changed since the
            // read above must cost them nothing but a trip back to the front.
            var seatable = _store.CurrentState == RideState.Loading
                ? Math.Min(taken.Count, _store.FreeSeatCount)
                : 0;

            if (seatable > 0)
            {
                var riders = new PassengerWeight[seatable];
                for (var i = 0; i < seatable; i++)
                {
                    riders[i] = new PassengerWeight(taken[i].WeightInKilograms);
                }

                _store.SeatSingleRiders(riders);
                seated = seatable;
            }
        }
        finally
        {
            if (seated < taken.Count)
            {
                // Not cancellable: abandoning the hand-back is exactly the lost guest
                // it exists to prevent.
                IReadOnlyList<PersonDto> unseated = seated == 0 ? taken : [.. taken.Skip(seated)];
                await queueService
                    .RequeueSingleRidersAsync(rideId, unseated, CancellationToken.None)
                    .ConfigureAwait(false);

                _logger.LogWarning(
                    "Returned {Count} un-seated single rider(s) to the front of ride {RideId}'s single-riders line.",
                    unseated.Count,
                    rideId);
            }
        }

        return seated;
    }

    private static Activity? StartPassActivity(Guid rideId)
    {
        var activity = BoogaBoosterTelemetry.ActivitySource.StartActivity(LoadingPassOperationName);
        activity?.SetTag(RideTelemetryAttributes.RideId, rideId);
        return activity;
    }

    /// <summary>
    /// The first group within the look-ahead window (the front three) whose members
    /// all fit the remaining <paramref name="emptyGondolas"/>, or <c>null</c> when
    /// none of them fit. A group of <c>N</c> needs <c>ceil(N / 2)</c> empty gondolas.
    /// </summary>
    private static QueuedGroupDto? FirstFittingGroup(IReadOnlyList<QueuedGroupDto> groups, int emptyGondolas)
    {
        var window = Math.Min(groups.Count, LookAheadWindow);
        for (var i = 0; i < window; i++)
        {
            var group = groups[i];
            var requiredGondolas = (group.Size + 1) / 2;
            if (requiredGondolas <= emptyGondolas)
            {
                return group;
            }
        }

        return null;
    }
}
