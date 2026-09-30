namespace FourDotnet.BoogaBooster.Queue.Observability;

/// <summary>
/// The span attribute names the Queue module records. Declared as constants in one
/// place so no call site repeats a literal, a rename is a single edit, and the tests
/// assert against the same name the production code writes.
/// </summary>
/// <remarks>
/// Every name here is a span attribute. The ride id in particular is deliberately
/// absent from the module's metric tags: a span is already one record per operation,
/// while a ride id as a metric tag would grow the series set without bound.
/// </remarks>
internal static class QueueTelemetryAttributes
{
    /// <summary>The ride whose line was addressed.</summary>
    internal const string RideId = "queue.ride.id";

    /// <summary>How many groups are waiting.</summary>
    internal const string GroupCount = "queue.group.count";

    /// <summary>How many people are waiting — a count, never the people themselves.</summary>
    internal const string PeopleWaiting = "queue.people.waiting";

    /// <summary>Groups a fill pass added to the line.</summary>
    internal const string GroupsAdded = "queue.groups.added";

    /// <summary>People a fill pass added to the line.</summary>
    internal const string PeopleAdded = "queue.people.added";

    /// <summary>The nice-weather reading an update carried.</summary>
    internal const string NiceWeatherObserved = "queue.weather.nice_weather.observed";

    /// <summary>The nice-weather reading the update replaced.</summary>
    internal const string NiceWeatherPrevious = "queue.weather.nice_weather.previous";
}
