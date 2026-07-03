using FourDotnet.BoogaBooster.Queue.Abstractions;
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
    /// Registers the Queue module's options, in-memory store, queue service and
    /// the background filler that keeps each ride's line populated.
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
        builder.Services.AddHostedService<RideQueueFillerService>();

        return builder;
    }
}
