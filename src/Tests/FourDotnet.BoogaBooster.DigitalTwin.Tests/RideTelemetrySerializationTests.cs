using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FourDotnet.BoogaBooster.DigitalTwin.Abstractions;
using FourDotnet.BoogaBooster.DigitalTwin.Application;
using FourDotnet.BoogaBooster.DigitalTwin.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace FourDotnet.BoogaBooster.DigitalTwin.Tests;

/// <summary>
/// Covers the wire shape of the live telemetry stream: the SSE result the endpoint
/// returns serialises each frame's rider experience as camelCase
/// <c>riderExperience</c> with its three averages — and as <c>null</c>s for an empty ride.
/// </summary>
public sealed class RideTelemetrySerializationTests
{
    [Fact]
    public async Task The_sse_frame_carries_the_rider_experience_in_camel_case()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        store.BoardGroup(
        [
            TestHelpers.Boarding(experience: new PassengerExperience(60d, 50d, 0d)),
            TestHelpers.Boarding(experience: new PassengerExperience(90d, 100d, 50d)),
        ]);

        using var frame = await FirstSseFrameAsync(store);

        var riderExperience = frame.RootElement.GetProperty("riderExperience");
        Assert.Equal(75d, riderExperience.GetProperty("averageHappiness").GetDouble());
        Assert.Equal(75d, riderExperience.GetProperty("averagePreferredIntensity").GetDouble());
        Assert.Equal(25d, riderExperience.GetProperty("averageNausea").GetDouble());
    }

    [Fact]
    public async Task An_empty_ride_streams_null_averages()
    {
        var store = new RideStore(new RandomRideEventSampler(seed: 1));
        store.RequestStateTransition(RideState.Loading); // active, so the stream emits, but nobody aboard

        using var frame = await FirstSseFrameAsync(store);

        var riderExperience = frame.RootElement.GetProperty("riderExperience");
        Assert.Equal(JsonValueKind.Null, riderExperience.GetProperty("averageHappiness").ValueKind);
        Assert.Equal(JsonValueKind.Null, riderExperience.GetProperty("averagePreferredIntensity").ValueKind);
        Assert.Equal(JsonValueKind.Null, riderExperience.GetProperty("averageNausea").ValueKind);
    }

    /// <summary>
    /// Executes the same SSE result the <c>/ride/telemetry/stream</c> endpoint returns,
    /// against an in-memory response, and parses the first frame's <c>data:</c> payload.
    /// </summary>
    private static async Task<JsonDocument> FirstSseFrameAsync(RideStore store)
    {
        var stream = new RideTelemetryStream(store, new FakeTimeProvider());
        var result = TypedResults.ServerSentEvents(
            FirstOnly(stream.Stream(TestContext.Current.CancellationToken)),
            eventType: "ride-telemetry");

        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider(),
        };
        context.Response.Body = body;

        await result.ExecuteAsync(context);

        var payload = Encoding.UTF8.GetString(body.ToArray());
        var data = payload
            .Split('\n')
            .Single(line => line.StartsWith("data:", StringComparison.Ordinal))["data:".Length..]
            .Trim();
        return JsonDocument.Parse(data);
    }

    /// <summary>Ends the otherwise endless telemetry stream after its first frame.</summary>
    private static async IAsyncEnumerable<RideTelemetry> FirstOnly(
        IAsyncEnumerable<RideTelemetry> frames,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var frame in frames.WithCancellation(cancellationToken))
        {
            yield return frame;
            yield break;
        }
    }
}
