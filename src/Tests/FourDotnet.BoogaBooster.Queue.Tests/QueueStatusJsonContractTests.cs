using System.Text.Json;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Pins the JSON names the dashboard reads from <c>GET /rides/{id}/queue</c>
/// (minimal APIs serialise with the web defaults, so camelCase): each person's
/// experience and the line's average happiness, which is <c>null</c> — not 0 — for
/// an empty line.
/// </summary>
public sealed class QueueStatusJsonContractTests
{
    [Fact]
    public void A_status_serialises_each_guests_experience_and_the_average_happiness()
    {
        var person = new PersonDto(7, "Person 7", 80, Happiness: 62.5, PreferredRideIntensity: 88, Nausea: 0);
        var status = new GetQueueStatusResponse(
            Guid.NewGuid(),
            GroupCount: 1,
            PeopleWaiting: 1,
            Groups: [new QueuedGroupDto(Guid.NewGuid(), [person])],
            AverageHappiness: 62.5);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(status, JsonSerializerOptions.Web));

        Assert.Equal(62.5, json.RootElement.GetProperty("averageHappiness").GetDouble());
        var guest = json.RootElement.GetProperty("groups")[0].GetProperty("people")[0];
        Assert.Equal(62.5, guest.GetProperty("happiness").GetDouble());
        Assert.Equal(88d, guest.GetProperty("preferredRideIntensity").GetDouble());
        Assert.Equal(0d, guest.GetProperty("nausea").GetDouble());
    }

    [Fact]
    public void An_empty_status_serialises_the_average_happiness_as_null()
    {
        var status = new GetQueueStatusResponse(Guid.NewGuid(), GroupCount: 0, PeopleWaiting: 0, Groups: [], AverageHappiness: null);

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(status, JsonSerializerOptions.Web));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("averageHappiness").ValueKind);
    }
}
