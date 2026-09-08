using FourDotnet.BoogaBooster.Core.Cqrs;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Abstractions.DataTransferObjects.GetQueueStatus;
using FourDotnet.BoogaBooster.Queue.Features.GetQueueStatus;
using FourDotnet.BoogaBooster.Queue.Features.RecordWeatherUpdate;
using FourDotnet.BoogaBooster.Queue.Filling;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FourDotnet.BoogaBooster.Queue;

/// <summary>
/// Composition entry point for the Queue module (ADR-0007). A host wires the
/// module up with <see cref="AddQueueModule"/> and
/// <see cref="Endpoints.QueueEndpoints.MapQueueEndpoints"/>. The integration-event
/// publisher is registered once at the host level via
/// <c>AddBoogaBoosterIntegrationMessages()</c>.
/// </summary>
public static class QueueModuleExtensions
{
    /// <summary>
    /// Registers the Queue module's options, in-memory store, queue service, the
    /// feature handlers, and the background filler that keeps each ride's line
    /// populated.
    /// </summary>
    public static IHostApplicationBuilder AddQueueModule(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<QueueModuleOptions>()
            .Bind(builder.Configuration.GetSection(QueueModuleOptions.SectionName));

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IRideQueueStore, InMemoryRideQueueStore>();
        builder.Services.AddSingleton<IPersonGenerator, PersonGenerator>();
        builder.Services.AddSingleton<IRideQueueService, RideQueueService>();
        // Shared, thread-safe weather state: the weather subscriber writes it, the
        // filler reads it each cycle to scale arrivals to the weather.
        builder.Services.AddSingleton<IWeatherInfluence, WeatherInfluence>();
        builder.Services.AddHostedService<RideQueueFillerService>();

        // Feature handlers (CQRS — ADR-0005/0006).
        builder.Services.AddScoped<IQueryHandler<GetQueueStatusQuery, GetQueueStatusResponse>, GetQueueStatusQueryHandler>();
        builder.Services.AddScoped<ICommandHandler<RecordWeatherUpdateCommand>, RecordWeatherUpdateCommandHandler>();

        return builder;
    }
}
