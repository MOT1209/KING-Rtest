using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using KINGRecorder.Capture;
using KINGRecorder.Capture.ScreenCapture;
using KINGRecorder.Core.Interfaces;

namespace KINGRecorder.Capture.Tests.DependencyInjection;

public class CaptureServiceRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddKingRecorderCapture();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    [Fact]
    public void AddKingRecorderCapture_RegistersOneSingletonInstanceForInterfaceAndConcreteType()
    {
        using var provider = BuildProvider();

        var service = provider.GetRequiredService<IScreenCaptureService>();
        var concrete = provider.GetRequiredService<Direct3D11CaptureEngine>();

        Assert.IsType<Direct3D11CaptureEngine>(service);
        Assert.Same(service, concrete);
    }

    [Fact]
    public void AddKingRecorderCapture_RegistersCaptureStateAsHonestlyIdle()
    {
        using var provider = BuildProvider();

        Assert.False(provider.GetRequiredService<IScreenCaptureService>().IsCapturing);
    }
}
