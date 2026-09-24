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
/// in that window fits (the ride is full) or the queue empties. Because a pass is
/// idempotent and re-runs every simulation tick, a group that joins the queue while
/// the ride is still loading boards on the next tick.
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

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var emptyGondolas = _store.EmptyGondolaCount;
                if (emptyGondolas <= 0)
                {
                    return; // The ride is full — no empty gondola left to seat a fresh group.
                }

                var status = _queueService.GetStatus(rideId);
                if (status.Groups.Count == 0)
                {
                    return; // Nobody waiting.
                }

                var chosen = FirstFittingGroup(status.Groups, emptyGondolas);
                if (chosen is null)
                {
                    return; // None of the first three waiting groups fit — the ride is full.
                }

                var taken = await _queueService
                    .TakeGroupAsync(rideId, chosen.GroupId, cancellationToken)
                    .ConfigureAwait(false);
                if (taken is null)
                {
                    continue; // The group was already gone; re-read the line and retry.
                }

                var members = ToPassengers(taken.People);
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
                activity.Dispose();
            }
        }
    }

    /// <summary>
    /// Turns the taken group's members into passengers carrying exactly the weight and
    /// the wait-adjusted mood the queue reported at take time — so what a rider boards
    /// with is what the queue said they felt when they were called (design D8).
    /// </summary>
    private static Passenger[] ToPassengers(IReadOnlyList<PersonDto> people)
    {
        var passengers = new Passenger[people.Count];
        for (var i = 0; i < passengers.Length; i++)
        {
            var person = people[i];
            passengers[i] = new Passenger(
                new PassengerWeight(person.WeightInKilograms),
                new RiderProfile(person.PreferredIntensity, person.Happiness, person.Nausea));
        }

        return passengers;
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
