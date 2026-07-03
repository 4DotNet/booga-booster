using System.Collections.Concurrent;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Application;
using FourDotnet.BoogaBooster.Weather.Domain;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

public sealed class WeatherStoreConcurrencyTests
{
    private static readonly TimeSpan Tick = WeatherDefaults.TickInterval;

    [Fact]
    public async Task Reads_during_advances_always_return_a_consistent_snapshot()
    {
        var store = new WeatherStore(new RandomWeatherSampler(seed: 7));
        store.StartStrongWind(); // keep the readings moving during the run

        var snapshots = new ConcurrentBag<WeatherConditionDto>();
        const int iterations = 5000;

        var writers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                store.Advance(Tick);
            }
        }));

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
            {
                snapshots.Add(store.GetSnapshot());
            }
        }));

        await Task.WhenAll(writers.Concat(readers));

        Assert.NotEmpty(snapshots);
        foreach (var snapshot in snapshots)
        {
            // A torn read would surface as an out-of-range or undefined value.
            Assert.InRange(snapshot.TemperatureCelsius, Temperature.MinCelsius, Temperature.MaxCelsius);
            Assert.InRange(snapshot.WindBeaufort, Wind.MinBeaufort, Wind.MaxBeaufort);
            Assert.InRange(snapshot.SunshinePercent, Sunshine.MinPercent, Sunshine.MaxPercent);
            Assert.True(Enum.IsDefined(snapshot.Precipitation));
            Assert.True(Enum.IsDefined(snapshot.Regime));
        }
    }
}
