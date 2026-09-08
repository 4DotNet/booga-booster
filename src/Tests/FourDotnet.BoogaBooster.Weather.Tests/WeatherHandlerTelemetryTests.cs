using System.Diagnostics;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Features.GetWeather;
using FourDotnet.BoogaBooster.Weather.Features.StartPrecipitation;
using FourDotnet.BoogaBooster.Weather.Features.StartStrongWind;
using FourDotnet.BoogaBooster.Weather.Tests.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

/// <summary>
/// Covers the span attributes the Weather handlers add on top of the base-class
/// instrumentation (ADR-0009), the disturbance counter both commands record, and the
/// span the simulation service emits per advance.
/// </summary>
public sealed class WeatherHandlerTelemetryTests : IDisposable
{
    private const string DisturbanceCounter = "boogabooster.weather.disturbances";
    private const string RegimeTag = "weather.regime";
    private const string NiceWeatherTag = "weather.nice_weather";

    private readonly TelemetryRecorder _telemetry = new();

    public void Dispose() => _telemetry.Dispose();

    [Fact]
    public async Task StartPrecipitation_TagsThePrecipitationAsked_AndTheRegimeItFound()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        var handler = new StartPrecipitationCommandHandler(store, NoOpPublisher());

        using (_telemetry.Scope())
        {
            await handler.HandleAsync(
                new StartPrecipitationCommand(PrecipitationType.Hail),
                TestContext.Current.CancellationToken);
        }

        var activity = _telemetry.Activity("StartPrecipitation");
        Assert.Equal(nameof(PrecipitationType.Hail), activity.GetTagItem("weather.precipitation.type"));

        // The regime is read before the store is mutated, so the span shows what the
        // command interrupted rather than what it caused.
        Assert.Equal(nameof(WeatherRegime.Calm), activity.GetTagItem(RegimeTag));
    }

    [Fact]
    public async Task StartPrecipitation_CountsTheDisturbance_AsPrecipitation()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        var handler = new StartPrecipitationCommandHandler(store, NoOpPublisher());

        await handler.HandleAsync(
            new StartPrecipitationCommand(PrecipitationType.Rain),
            TestContext.Current.CancellationToken);

        var measurement = _telemetry.Measurement(
            DisturbanceCounter,
            ("weather.disturbance.kind", "precipitation"));
        Assert.Equal(1, measurement.Value);

        // Kind is the only tag: it is the one value drawn from a fixed set here.
        Assert.Equal(["weather.disturbance.kind"], measurement.Tags.Keys);
    }

    [Fact]
    public async Task StartStrongWind_TagsTheConditionsItFound_ThoughItCarriesNoPayload()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        var handler = new StartStrongWindCommandHandler(store, NoOpPublisher());
        var before = store.GetSnapshot();

        using (_telemetry.Scope())
        {
            await handler.HandleAsync(new StartStrongWindCommand(), TestContext.Current.CancellationToken);
        }

        // A payload-free command would otherwise leave every invocation identical
        // (design D2), so the span carries the wind already blowing and the regime.
        var activity = _telemetry.Activity("StartStrongWind");
        Assert.Equal(nameof(WeatherRegime.Calm), activity.GetTagItem(RegimeTag));
        Assert.Equal(before.WindBeaufort, activity.GetTagItem("weather.wind.beaufort"));
    }

    [Fact]
    public async Task StartStrongWind_CountsTheDisturbance_AsStrongWind()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        var handler = new StartStrongWindCommandHandler(store, NoOpPublisher());

        await handler.HandleAsync(new StartStrongWindCommand(), TestContext.Current.CancellationToken);

        var measurement = _telemetry.Measurement(
            DisturbanceCounter,
            ("weather.disturbance.kind", "strong-wind"));
        Assert.Equal(1, measurement.Value);
    }

    [Fact]
    public async Task GetWeather_TagsTheConditionsItRead_ThoughItCarriesNoPayload()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        store.StartPrecipitation(PrecipitationType.Snow);
        var expected = store.GetSnapshot();
        var handler = new GetWeatherQueryHandler(store);

        using (_telemetry.Scope())
        {
            await handler.HandleAsync(new GetWeatherQuery(), TestContext.Current.CancellationToken);
        }

        var activity = _telemetry.Activity("GetWeather");
        Assert.Equal(nameof(WeatherRegime.Precipitation), activity.GetTagItem(RegimeTag));
        Assert.Equal(expected.NiceWeather, activity.GetTagItem(NiceWeatherTag));
    }

    [Fact]
    public async Task GetWeather_LeavesResponseTagsOff_WhenTheReadFails()
    {
        var store = new Mock<IWeatherStore>();
        store.Setup(s => s.GetSnapshot()).Throws(new InvalidOperationException("no store"));
        var handler = new GetWeatherQueryHandler(store.Object);

        using (_telemetry.Scope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => handler.HandleAsync(new GetWeatherQuery(), TestContext.Current.CancellationToken));
        }

        var activity = _telemetry.Activity("GetWeather");
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Null(activity.GetTagItem(RegimeTag));
    }

    [Fact]
    public async Task TheSimulationService_SpansEachAdvance_WithTheResultingConditions()
    {
        var store = new WeatherStore(ConstantSampler.Zero);
        store.StartStrongWind();
        var service = SimulationService(store);

        using (_telemetry.Scope())
        {
            await service.TickAsync(TestContext.Current.CancellationToken);
        }

        var activity = _telemetry.Activity("AdvanceWeather");
        var after = store.GetSnapshot();
        Assert.Equal(after.Regime.ToString(), activity.GetTagItem(RegimeTag));
        Assert.Equal(after.NiceWeather, activity.GetTagItem(NiceWeatherTag));
        Assert.NotNull(activity.GetTagItem("weather.changed"));
    }

    [Fact]
    public async Task TheSimulationService_RecordsAFailingAdvance_AsAnError()
    {
        var store = new Mock<IWeatherStore>();
        store.Setup(s => s.Advance(It.IsAny<TimeSpan>())).Throws(new InvalidOperationException("store gone"));
        var service = SimulationService(store.Object);

        using (_telemetry.Scope())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.TickAsync(TestContext.Current.CancellationToken));
        }

        // Without this the loop's log-and-continue would hide a broken advance.
        var activity = _telemetry.Activity("AdvanceWeather");
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("store gone", activity.StatusDescription);
        Assert.Contains(activity.Events, activityEvent => activityEvent.Name == "exception");
    }

    private static WeatherSimulationService SimulationService(IWeatherStore store)
        => new(
            store,
            NoOpPublisher(),
            TimeProvider.System,
            NullLogger<WeatherSimulationService>.Instance);

    private static IWeatherUpdatePublisher NoOpPublisher() => new NoOpWeatherUpdatePublisher();
}
