using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FourDotnet.BoogaBooster.IntegrationMessages;

/// <summary>
/// Registration entry point for the BoogaBooster integration-messaging library.
/// </summary>
public static class IntegrationMessagesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Dapr client and the integration-event publisher so any
    /// component can inject <see cref="IIntegrationEventPublisher"/> to publish
    /// integration events.
    /// </summary>
    public static IHostApplicationBuilder AddBoogaBoosterIntegrationMessages(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.AddBoogaBoosterIntegrationMessages();

        return builder;
    }

    /// <summary>
    /// Registers the Dapr client and the integration-event publisher into the
    /// service collection.
    /// </summary>
    public static IServiceCollection AddBoogaBoosterIntegrationMessages(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddDaprClient();
        services.AddSingleton<IIntegrationEventPublisher, DaprIntegrationEventPublisher>();

        return services;
    }
}
