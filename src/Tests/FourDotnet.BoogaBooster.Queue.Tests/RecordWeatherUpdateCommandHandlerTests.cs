using FourDotnet.BoogaBooster.Queue.Features.RecordWeatherUpdate;
using FourDotnet.BoogaBooster.Queue.Filling;
using Microsoft.Extensions.Options;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Covers the RecordWeatherUpdate feature handler and the shared
/// <see cref="WeatherInfluence"/> state it writes: the recorded value, clamping
/// into the unit interval, and newest-wins for out-of-order events.
/// </summary>
public sealed class RecordWeatherUpdateCommandHandlerTests
{
    private static (RecordWeatherUpdateCommandHandler handler, WeatherInfluence influence) Create(
        double neutralDefault = 1.0)
    {
        var influence = new WeatherInfluence(
            Options.Create(new QueueModuleOptions { NeutralWeatherDefault = neutralDefault }));

        return (new RecordWeatherUpdateCommandHandler(influence), influence);
    }

    [Fact]
    public async Task HandleAsync_RecordsTheCommandNiceWeather()
    {
        var (handler, influence) = Create();

        await handler.HandleAsync(
            new RecordWeatherUpdateCommand(0.4f),
            TestContext.Current.CancellationToken);

        Assert.Equal(0.4, influence.Current, precision: 5);
    }

    [Theory]
    [InlineData(1.7f, 1.0)]
    [InlineData(-0.5f, 0.0)]
    public async Task HandleAsync_ClampsOutOfRangeValues_IntoUnitInterval(float recorded, double expected)
    {
        var (handler, influence) = Create();

        await handler.HandleAsync(
            new RecordWeatherUpdateCommand(recorded),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, influence.Current, precision: 5);
    }

    [Fact]
    public async Task HandleAsync_NewestCommandWins_RegardlessOfOrder()
    {
        var (handler, influence) = Create();

        await handler.HandleAsync(new RecordWeatherUpdateCommand(0.8f), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new RecordWeatherUpdateCommand(0.2f), TestContext.Current.CancellationToken);

        Assert.Equal(0.2, influence.Current, precision: 5);
    }

    [Fact]
    public async Task HandleAsync_NullCommand_Throws()
    {
        var (handler, _) = Create();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            handler.HandleAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_NullInfluence_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new RecordWeatherUpdateCommandHandler(null!));
    }
}
