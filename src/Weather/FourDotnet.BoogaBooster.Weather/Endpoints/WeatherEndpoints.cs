using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Weather.Abstractions;
using FourDotnet.BoogaBooster.Weather.Features.GetWeather;
using FourDotnet.BoogaBooster.Weather.Features.StartPrecipitation;
using FourDotnet.BoogaBooster.Weather.Features.StartStrongWind;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FourDotnet.BoogaBooster.Weather.Endpoints;

/// <summary>
/// HTTP endpoints owned by the Weather module (ADR-0007). The API host maps them
/// by calling <see cref="MapWeatherEndpoints"/>; it contains no endpoint mappings
/// itself. Each endpoint maps its request to a command/query and dispatches to the
/// injected handler — no business logic lives here.
/// </summary>
public static class WeatherEndpoints
{
    /// <summary>The request body for starting a precipitation event.</summary>
    /// <param name="Type">The precipitation type: <c>Rain</c>, <c>Snow</c>, or <c>Hail</c>.</param>
    public sealed record StartPrecipitationRequest(string? Type);

    public static IEndpointRouteBuilder MapWeatherEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/weather").WithTags("Weather");

        group.MapGet("/", async (
            IQueryHandler<GetWeatherQuery, WeatherConditionDto> handler,
            CancellationToken cancellationToken) =>
        {
            var conditions = await handler.HandleAsync(new GetWeatherQuery(), cancellationToken);
            return Results.Ok(conditions);
        })
        .WithName("GetWeather");

        group.MapPost("/precipitation", async (
            StartPrecipitationRequest request,
            ICommandHandler<StartPrecipitationCommand> handler,
            CancellationToken cancellationToken) =>
        {
            if (!TryParsePrecipitationType(request.Type, out var type))
            {
                return Results.BadRequest(
                    $"Unknown precipitation type '{request.Type}'. Expected Rain, Snow, or Hail.");
            }

            await handler.HandleAsync(new StartPrecipitationCommand(type), cancellationToken);
            return Results.Accepted();
        })
        .WithName("StartPrecipitation");

        group.MapPost("/strong-wind", async (
            ICommandHandler<StartStrongWindCommand> handler,
            CancellationToken cancellationToken) =>
        {
            await handler.HandleAsync(new StartStrongWindCommand(), cancellationToken);
            return Results.Accepted();
        })
        .WithName("StartStrongWind");

        return endpoints;
    }

    private static bool TryParsePrecipitationType(string? value, out PrecipitationType type)
    {
        if (Enum.TryParse(value, ignoreCase: true, out type)
            && Enum.IsDefined(type)
            && type != PrecipitationType.None)
        {
            return true;
        }

        type = PrecipitationType.None;
        return false;
    }
}
