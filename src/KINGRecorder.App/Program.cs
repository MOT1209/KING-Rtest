using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using KINGRecorder.Audio;
using KINGRecorder.Capture.ScreenCapture;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Infrastructure;
using KINGRecorder.Recording;
using KINGRecorder.Recording.Stubs;
using KINGRecorder.App.ViewModels;

namespace KINGRecorder.App;

public static class Program
{
    public static IServiceProvider Services { get; private set; } = null!;

    [STAThread]
    public static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        Application.Start((p) =>
        {
            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Core & Infrastructure
        services.AddSingleton<SettingsManager>();

        // Hardware Services
        services.AddSingleton<IScreenCaptureService, Direct3D11CaptureEngine>();
        services.AddSingleton<IAudioCaptureService, WasapiAudioCaptureService>();
        services.AddSingleton<IVideoCompositor, UnavailableVideoCompositor>();
        services.AddSingleton<IEncoderService, UnavailableEncoderService>();

        // Recording Engine
        services.AddSingleton<IRecordingEngine, RecordingEngine>();

        // ViewModels
        services.AddTransient<MainViewModel>();
    }
}
