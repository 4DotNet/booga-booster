using System.Diagnostics.Metrics;
using FourDotnet.BoogaBooster.Core.Observability;
using Xunit;

namespace FourDotnet.BoogaBooster.Core.Tests;

/// <summary>
/// Covers the domain instruments the modules publish through the one shared
/// <see cref="BoogaBoosterTelemetry.Meter"/> (ADR-0009): that each is declared on
/// that meter under the agreed name and unit, and that a measurement recorded
/// against it reaches a listener attached to the shared meter name — the spec's
/// "all metrics share one meter" requirement.
/// </summary>
public sealed class DomainInstrumentTests : IDisposable
{
    private readonly List<(string Instrument, double Value)> _measurements = [];
    private readonly MeterListener _listener;

    public DomainInstrumentTests()
    {
        _listener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == BoogaBoosterTelemetry.SourceName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            },
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => Record(instrument, value));
        _listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => Record(instrument, value));
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();

    /// <summary>
    /// Every instrument the solution declares, with the name and unit it must keep. A
    /// rename that misses a dashboard is a breaking change, so the names are pinned
    /// here by test.
    /// </summary>
    private static readonly (string Name, string Unit, string Member)[] Declared =
    [
        ("boogabooster.handler.invocations", "{invocation}", nameof(BoogaBoosterTelemetry.HandlerInvocations)),
        ("boogabooster.handler.duration", "ms", nameof(BoogaBoosterTelemetry.HandlerDuration)),
        ("boogabooster.ride.simulation.ticks", "{tick}", nameof(BoogaBoosterTelemetry.SimulationTicks)),
        ("boogabooster.ride.simulation.tick.duration", "ms", nameof(BoogaBoosterTelemetry.SimulationTickDuration)),
        ("boogabooster.ride.passengers.boarded", "{passenger}", nameof(BoogaBoosterTelemetry.PassengersBoarded)),
        ("boogabooster.ride.state.transitions", "{transition}", nameof(BoogaBoosterTelemetry.RideStateTransitions)),
        ("boogabooster.queue.groups.queued", "{group}", nameof(BoogaBoosterTelemetry.QueueGroupsQueued)),
        ("boogabooster.queue.people.queued", "{person}", nameof(BoogaBoosterTelemetry.QueuePeopleQueued)),
        ("boogabooster.weather.disturbances", "{disturbance}", nameof(BoogaBoosterTelemetry.WeatherDisturbances)),
        ("boogabooster.messaging.events.published", "{event}", nameof(BoogaBoosterTelemetry.IntegrationEventsPublished)),
    ];

    public static TheoryData<string, string, string> Instruments
    {
        get
        {
            var data = new TheoryData<string, string, string>();

            foreach (var (name, unit, member) in Declared)
            {
                data.Add(name, unit, member);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Instruments))]
    public void Instrument_IsDeclaredOnTheSharedMeter_UnderItsAgreedNameAndUnit(
        string name,
        string unit,
        string member)
    {
        var instrument = InstrumentNamed(name);

        Assert.NotNull(instrument);
        Assert.Equal(BoogaBoosterTelemetry.SourceName, instrument.Meter.Name);
        Assert.Equal(unit, instrument.Unit);
        Assert.False(string.IsNullOrWhiteSpace(instrument.Description), $"{member} needs a description.");
    }

    [Fact]
    public void EveryMetricName_IsPrefixedForTheSolution()
    {
        // The naming convention: every metric this solution owns starts "boogabooster.".
        Assert.All(
            Declared.Select(instrument => instrument.Name),
            name => Assert.StartsWith("boogabooster.", name, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryDomainCounter_ReachesAListenerOnTheSharedMeter()
    {
        BoogaBoosterTelemetry.SimulationTicks.Add(1);
        BoogaBoosterTelemetry.PassengersBoarded.Add(2);
        BoogaBoosterTelemetry.RideStateTransitions.Add(1);
        BoogaBoosterTelemetry.QueueGroupsQueued.Add(1);
        BoogaBoosterTelemetry.QueuePeopleQueued.Add(4);
        BoogaBoosterTelemetry.WeatherDisturbances.Add(1);
        BoogaBoosterTelemetry.IntegrationEventsPublished.Add(1);

        Assert.Contains(("boogabooster.ride.simulation.ticks", 1d), Recorded());
        Assert.Contains(("boogabooster.ride.passengers.boarded", 2d), Recorded());
        Assert.Contains(("boogabooster.ride.state.transitions", 1d), Recorded());
        Assert.Contains(("boogabooster.queue.groups.queued", 1d), Recorded());
        Assert.Contains(("boogabooster.queue.people.queued", 4d), Recorded());
        Assert.Contains(("boogabooster.weather.disturbances", 1d), Recorded());
        Assert.Contains(("boogabooster.messaging.events.published", 1d), Recorded());
    }

    [Fact]
    public void TheTickHistogram_ReachesAListenerOnTheSharedMeter()
    {
        BoogaBoosterTelemetry.SimulationTickDuration.Record(8.25);

        Assert.Contains(("boogabooster.ride.simulation.tick.duration", 8.25), Recorded());
    }

    [Fact]
    public void TheActivitySourceAndMeter_ShareOneWellKnownName()
    {
        // ADR-0009 mandates a single source and meter name per service, and
        // ServiceDefaults registers exactly this one name as both.
        Assert.Equal(BoogaBoosterTelemetry.SourceName, BoogaBoosterTelemetry.ActivitySource.Name);
        Assert.Equal(BoogaBoosterTelemetry.SourceName, BoogaBoosterTelemetry.Meter.Name);
    }

    /// <summary>
    /// Finds the published instrument by name. Declared instruments publish on the
    /// listener's <see cref="MeterListener.Start"/>, so a static-field initialiser is
    /// enough for it to be found — no measurement required.
    /// </summary>
    private Instrument? InstrumentNamed(string name)
    {
        Instrument? found = null;

        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, _) =>
            {
                if (instrument.Meter.Name == BoogaBoosterTelemetry.SourceName && instrument.Name == name)
                {
                    found = instrument;
                }
            },
        };
        listener.Start();

        return found;
    }

    private void Record(Instrument instrument, double value)
    {
        lock (_measurements)
        {
            _measurements.Add((instrument.Name, value));
        }
    }

    private List<(string Instrument, double Value)> Recorded()
    {
        lock (_measurements)
        {
            return [.. _measurements];
        }
    }
}
