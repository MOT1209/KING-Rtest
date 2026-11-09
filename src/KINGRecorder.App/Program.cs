using System;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using KINGRecorder.App.Services;
using KINGRecorder.Audio;
using KINGRecorder.Capture;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Infrastructure;
using KINGRecorder.Presentation.Dispatching;
using KINGRecorder.Presentation.ViewModels;
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
            // The engine must finish cleaning up its session *before* the container disposes the capture,
            // audio and encoder services it collaborates with. Application.Start has already returned, so
            // the XAML message loop no longer needs this thread, and every engine await uses
            // ConfigureAwait(false), which is why blocking on the shutdown here cannot deadlock.
            ShutDownRecordingEngine(Services);

            // The composition root owns every disposable service, including the capture/audio/encoder
            // instances that RecordingEngine collaborates with. Release them exactly once on exit.
            (Services as IDisposable)?.Dispose();
        }
    }

    private static void ShutDownRecordingEngine(IServiceProvider services)
    {
        // Resolved lazily: the engine may never have been created if the window failed to open.
        if (services.GetService<IRecordingEngine>() is not { } engine)
        {
            return;
        }

        try
        {
            engine.ShutdownAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Shutdown must not take the process down on exit; the container is disposed next regardless.
            services.GetService<ILoggerFactory>()
                ?.CreateLogger("KINGRecorder.App.Program")
                .LogError(ex, "Recording engine shutdown did not complete cleanly.");
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

        // UI thread access for the ViewModel. The WinUI DispatcherQueue only exists once the XAML thread is
        // running, and this factory is resolved from OnLaunched on that thread, so it captures the real UI
        // queue instead of relying on a global static.
        services.AddSingleton<IUiDispatcher>(static provider =>
        {
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue is null)
            {
                throw new InvalidOperationException(
                    "No UI thread DispatcherQueue is available; MainViewModel must be resolved from the XAML thread.");
            }

            return new DispatcherQueueUiDispatcher(
                dispatcherQueue,
                provider.GetRequiredService<ILogger<DispatcherQueueUiDispatcher>>());
        });

        // Presentation
        services.AddTransient<MainViewModel>();
    }
}
