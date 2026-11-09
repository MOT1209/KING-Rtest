using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using KINGRecorder.App.ViewModels;
using KINGRecorder.Audio;
using KINGRecorder.Capture;
using KINGRecorder.Infrastructure;
using KINGRecorder.Recording;

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

        Services = serviceCollection.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        try
        {
            Application.Start((p) =>
            {
                var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
                SynchronizationContext.SetSynchronizationContext(context);
                _ = new App();
            });
        }
        finally
        {
            // The composition root owns every disposable service, including the capture/audio/encoder
            // instances that RecordingEngine collaborates with. Release them exactly once on exit.
            (Services as IDisposable)?.Dispose();
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Layer registrations live next to their implementations so they can be verified by unit tests.
        services.AddKingRecorderInfrastructure();
        services.AddKingRecorderCapture();
        services.AddKingRecorderAudio();
        services.AddKingRecorderRecording();

        // Presentation
        services.AddTransient<MainViewModel>();
    }
}
