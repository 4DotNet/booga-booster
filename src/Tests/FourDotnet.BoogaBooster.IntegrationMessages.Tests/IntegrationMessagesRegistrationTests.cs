using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace FourDotnet.BoogaBooster.IntegrationMessages.Tests;

/// <summary>
/// Covers the registration entry points that wire up the integration-event
/// publisher so any component can inject <see cref="IIntegrationEventPublisher"/>.
/// </summary>
public class IntegrationMessagesRegistrationTests
{
    [Fact]
    public void AddToServices_RegistersThePublisher()
    {
        var services = new ServiceCollection();

        services.AddBoogaBoosterIntegrationMessages();

        Assert.Contains(services, d => d.ServiceType == typeof(IIntegrationEventPublisher));
    }

    [Fact]
    public void AddToServices_ReturnsSameCollection_ForChaining()
    {
        var services = new ServiceCollection();

        var result = services.AddBoogaBoosterIntegrationMessages();

        Assert.Same(services, result);
    }

    [Fact]
    public void AddToBuilder_RegistersThePublisher()
    {
        var builder = Host.CreateApplicationBuilder();

        var result = builder.AddBoogaBoosterIntegrationMessages();

        Assert.Same(builder, result);
        Assert.Contains(builder.Services, d => d.ServiceType == typeof(IIntegrationEventPublisher));
    }

    [Fact]
    public void AddToServices_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ((IServiceCollection)null!).AddBoogaBoosterIntegrationMessages());
    }

    [Fact]
    public void AddToBuilder_NullBuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => ((IHostApplicationBuilder)null!).AddBoogaBoosterIntegrationMessages());
    }
}
