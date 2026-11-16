using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KINGRecorder.Capture.ScreenCapture;
using KINGRecorder.Core.Interfaces;

namespace KINGRecorder.Capture;

/// <summary>
/// Composition-root helper for the capture layer. Kept free of WinUI types so it can be exercised by tests.
/// </summary>
public static class CaptureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the screen-capture service. <see cref="Direct3D11CaptureEngine.StartCaptureAsync"/> now builds
    /// the Direct3D 11 device, WinRT device and Windows.Graphics.Capture frame pool/session (Phase 1 task 3);
    /// capture-item creation is limited by the Windows 10 2004 SDK projection and throws a structured
    /// <see cref="NotSupportedException"/> until a newer SDK (>= 10.0.22000.0) is adopted.
    /// </summary>
    public static IServiceCollection AddKingRecorderCapture(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<Direct3D11CaptureEngine>();
        services.TryAddSingleton<IScreenCaptureService>(static provider => provider.GetRequiredService<Direct3D11CaptureEngine>());

        return services;
    }
}
