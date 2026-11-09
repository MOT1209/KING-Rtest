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
    /// Registers the screen-capture service. <see cref="Direct3D11CaptureEngine.StartCaptureAsync"/> still
    /// throws <see cref="NotSupportedException"/> until the Windows Graphics Capture pipeline exists (Phase 1);
    /// the registration does not pretend otherwise.
    /// </summary>
    public static IServiceCollection AddKingRecorderCapture(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<Direct3D11CaptureEngine>();
        services.TryAddSingleton<IScreenCaptureService>(static provider => provider.GetRequiredService<Direct3D11CaptureEngine>());

        return services;
    }
}
