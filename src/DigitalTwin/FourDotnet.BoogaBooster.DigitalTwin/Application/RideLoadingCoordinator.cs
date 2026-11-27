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
/// in that window fits or the queue empties. Only then does the pass top up every
/// remaining free seat — including the seat beside a lone group member — with single
/// riders, in arrival order. Because a pass is idempotent and re-runs every
/// simulation tick, a group or single rider that joins while the ride is still
/// loading boards on a later tick.
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
            // Phase 1 — groups, exactly as before single riders existed. Every stop
            // condition ends only this phase: the boundary "no waiting group fits any
            // more" (including an empty group queue) is where phase 2 begins.
            while (!cancellationToken.IsCancellationRequested)
            {
                var emptyGondolas = _store.EmptyGondolaCount;
                if (emptyGondolas <= 0)
                {
                    break; // No empty gondola left to seat a fresh group.
                }

                var status = _queueService.GetStatus(rideId);
                if (status.Groups.Count == 0)
                {
                    break; // No group waiting.
                }

                var chosen = FirstFittingGroup(status.Groups, emptyGondolas);
                if (chosen is null)
                {
                    break; // None of the first three waiting groups fit.
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

            // Phase 2 — top up every remaining free seat with single riders.
            singleRidersSeated = await TopUpWithSingleRidersAsync(rideId, cancellationToken).ConfigureAwait(false);
            if (singleRidersSeated > 0)
            {
                activity ??= StartPassActivity(rideId);
                passengersBoarded += singleRidersSeated;
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
    /// The second phase of a pass: once no waiting group fits, seats single riders —
    /// in arrival order — into every remaining free seat, including the seat beside a
    /// passenger in a partly-occupied gondola. A silent no-op when the ride has left
    /// <see cref="RideState.Loading"/>, has no free seat, or nobody is in the
    /// single-riders line. Riders taken but not seated go back to the front of the
    /// line, so a failed seating never loses a guest or their place.
    /// </summary>
    /// <returns>The number of single riders seated.</returns>
    private async Task<int> TopUpWithSingleRidersAsync(Guid rideId, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested || _store.CurrentState != RideState.Loading)
        {
            return 0;
        }

        var freeSeats = _store.FreeSeatCount;
        if (freeSeats <= 0)
        {
            return 0; // The ride is full.
        }

        var taken = await _queueService!
            .TakeSingleRidersAsync(rideId, freeSeats, cancellationToken)
            .ConfigureAwait(false);
        if (taken.Count == 0)
        {
            return 0; // Nobody in the single-riders line.
        }

        // Re-read the live capacity right before seating: the store lock is the arbiter,
        // and anyone who no longer fits is returned rather than over-seated.
        var seatable = Math.Min(taken.Count, _store.FreeSeatCount);
        var seated = 0;

        try
        {
            if (seatable > 0)
            {
                var riders = taken
                    .Take(seatable)
                    .Select(person => new PassengerWeight(person.WeightInKilograms))
                    .ToArray();
                _store.SeatSingleRiders(riders);
                seated = seatable;
            }
        }
        finally
        {
            if (seated < taken.Count)
            {
                // Not cancellable: these guests must never be dropped on the floor.
                await _queueService
                    .RequeueSingleRidersAsync(rideId, [.. taken.Skip(seated)], CancellationToken.None)
                    .ConfigureAwait(false);
            }
        }

        if (seated == 0)
        {
            return 0;
        }

        BoogaBoosterTelemetry.PassengersBoarded.Add(seated);

        _logger.LogInformation(
            "Seated {Count} single rider(s) onto ride {RideId}; {FreeSeats} seat(s) still free.",
            seated,
            rideId,
            _store.FreeSeatCount);

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
