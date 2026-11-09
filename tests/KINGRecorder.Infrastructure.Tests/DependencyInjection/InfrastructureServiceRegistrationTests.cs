using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using KINGRecorder.Infrastructure;

namespace KINGRecorder.Infrastructure.Tests.DependencyInjection;

public class InfrastructureServiceRegistrationTests
{
    [Fact]
    public void AddKingRecorderInfrastructure_RegistersSettingsManagerAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddKingRecorderInfrastructure();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var first = provider.GetRequiredService<SettingsManager>();
        var second = provider.GetRequiredService<SettingsManager>();

        Assert.Same(first, second);
    }
}
