using System.Reflection;
using FourDotnet.BoogaBooster.Core.Cqrs;
using Xunit;

namespace FourDotnet.BoogaBooster.Weather.Tests;

/// <summary>
/// Turns ADR-0009's per-handler tagging contract into a build gate for this module:
/// every command and query handler it ships must contribute attributes of its own, so
/// a handler added — or an <c>EnrichActivity</c> override deleted — without
/// instrumentation fails the build rather than waiting for an audit to find it.
/// </summary>
public sealed class WeatherHandlerInstrumentationGateTests
{
    /// <summary>Every concrete handler this module's library declares.</summary>
    public static TheoryData<Type> Handlers
    {
        get
        {
            var data = new TheoryData<Type>();

            foreach (var handler in HandlerTypes())
            {
                data.Add(handler);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Handlers))]
    public void EveryHandler_ContributesItsOwnSpanAttributes(Type handler)
    {
        // A payload-free command still records the state its work depended on, and a
        // payload-free query still describes its response, so no handler is exempt —
        // which is why this asserts an override exists rather than allowing a no-op.
        var overrides = handler
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Where(method => method.Name is "EnrichActivity" or "EnrichActivityWithResponse")
            .Select(method => method.Name)
            .ToList();

        Assert.True(
            overrides.Count > 0,
            $"{handler.Name} overrides neither EnrichActivity nor EnrichActivityWithResponse. "
                + "ADR-0009 requires every handler to tag its span with the identifiers and "
                + "inputs that matter for diagnosis.");
    }

    [Fact]
    public void TheModuleShipsHandlers_SoTheGateIsNotVacuouslyGreen()
    {
        // Without this, deleting every handler would make the theory above pass by
        // having nothing to check.
        Assert.NotEmpty(HandlerTypes());
    }

    private static List<Type> HandlerTypes()
        => [.. typeof(FourDotnet.BoogaBooster.Weather.Features.GetWeather.GetWeatherQueryHandler).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true } && DerivesFromHandlerBase(type))
            .OrderBy(type => type.Name, StringComparer.Ordinal)];

    private static bool DerivesFromHandlerBase(Type type)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (!current.IsGenericType)
            {
                continue;
            }

            var definition = current.GetGenericTypeDefinition();
            if (definition == typeof(CommandHandler<>) || definition == typeof(QueryHandler<,>))
            {
                return true;
            }
        }

        return false;
    }
}
