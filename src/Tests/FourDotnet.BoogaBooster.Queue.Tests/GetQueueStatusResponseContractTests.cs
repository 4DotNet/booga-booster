using System.Text.Json;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Pins the wire shape of <c>GET /rides/{rideId}/queue</c> that the Angular queue
/// panel reads: the single-riders line travels in the same response as the groups,
/// under the camel-cased names minimal APIs serialize with by default.
/// </summary>
public sealed class GetQueueStatusResponseContractTests
{
    [Fact]
    public void Response_SerializesBothLines_UnderTheNamesTheFrontendReads()
    {
        var response = new GetQueueStatusResponse(
            Guid.NewGuid(),
            GroupCount: 1,
            PeopleWaiting: 2,
            Groups: [new QueuedGroupDto(Guid.NewGuid(), [new PersonDto(1, "A", 70), new PersonDto(2, "B", 80)])],
            SingleRidersWaiting: 1,
            SingleRiders: [new PersonDto(3, "C", 90)]);

        using var json = JsonDocument.Parse(
            JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var root = json.RootElement;

        Assert.Equal(1, root.GetProperty("groupCount").GetInt32());
        Assert.Equal(2, root.GetProperty("peopleWaiting").GetInt32());
        Assert.Equal(1, root.GetProperty("groups").GetArrayLength());
        Assert.Equal(1, root.GetProperty("singleRidersWaiting").GetInt32());
        Assert.Equal(3, root.GetProperty("singleRiders")[0].GetProperty("number").GetInt64());
        Assert.Equal(3, root.GetProperty("totalPeopleWaiting").GetInt32());
    }
}
