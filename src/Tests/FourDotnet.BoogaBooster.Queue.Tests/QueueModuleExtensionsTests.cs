using FourDotnet.BoogaBooster.IntegrationMessages;
using FourDotnet.BoogaBooster.Queue.Abstractions;
using FourDotnet.BoogaBooster.Queue.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace FourDotnet.BoogaBooster.Queue.Tests;

/// <summary>
/// Verifies the module composition entry point (ADR-0007): a single
/// <see cref="QueueModuleExtensions.AddQueueModule"/> call registers everything
/// the Queue module needs.
/// </summary>
public class QueueModuleExtensionsTests
{
    private static IHostApplicationBuilder BuildHost()
    {
        var builder = Host.CreateApplicationBuilder();
        // The integration-message publisher is a host-level dependency (registered by
        // AddBoogaBoosterIntegrationMessages); provide a stand-in so the queue service resolves.
        builder.Services.AddSingleton(new Mock<IIntegrationEventPublisher>().Object);
        builder.AddQueueModule();
        return builder;
    }

    [Fact]
    public void AddQueueModule_RegistersStoreServiceAndFiller()
    {
        var builder = BuildHost();

        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IRideQueueStore));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IRideQueueService));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IHostedService));
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(TimeProvider));
    }

    [Fact]
    public void AddQueueModule_ResolvesCoreServices()
    {
        var builder = BuildHost();
        using var provider = builder.Services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IRideQueueStore>());
        Assert.NotNull(provider.GetRequiredService<IRideQueueService>());
        // Store is a singleton — the filler and the API must share the same instance.
        Assert.Same(
            provider.GetRequiredService<IRideQueueStore>(),
            provider.GetRequiredService<IRideQueueStore>());
    }

    [Fact]
    public void AddQueueModule_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IHostApplicationBuilder)null!).AddQueueModule());
    }
}
