using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Recording;
using KINGRecorder.Recording.Stubs;
using KINGRecorder.Recording.Tests.Fakes;

namespace KINGRecorder.Recording.Tests.DependencyInjection;

public class RecordingServiceRegistrationTests
{
    private static ServiceProvider BuildProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IScreenCaptureService, FakeScreenCaptureService>();
        services.AddSingleton<IAudioCaptureService, FakeAudioCaptureService>();

        configure?.Invoke(services);
        services.AddKingRecorderRecording();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    [Fact]
    public void AddKingRecorderRecording_RegistersExplicitlyUnavailablePipelineServices()
    {
        using var provider = BuildProvider();

        Assert.IsType<UnavailableVideoCompositor>(provider.GetRequiredService<IVideoCompositor>());
        Assert.IsType<UnavailableEncoderService>(provider.GetRequiredService<IEncoderService>());
    }

    [Fact]
    public void AddKingRecorderRecording_RegistersRecordingEngineAsSingleton()
    {
        using var provider = BuildProvider();

        var first = provider.GetRequiredService<IRecordingEngine>();
        var second = provider.GetRequiredService<IRecordingEngine>();

        Assert.IsType<RecordingEngine>(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void AddKingRecorderRecording_DoesNotOverrideExistingPipelineRegistrations()
    {
        var replacementEncoder = new FakeEncoderService();

        using var provider = BuildProvider(services => services.AddSingleton<IEncoderService>(replacementEncoder));

        Assert.Same(replacementEncoder, provider.GetRequiredService<IEncoderService>());
    }
}
