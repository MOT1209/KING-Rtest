using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KINGRecorder.Core.Interfaces;

namespace KINGRecorder.Audio;

/// <summary>
/// Composition-root helper for the audio layer. Kept free of WinUI types so it can be exercised by tests.
/// </summary>
public static class AudioServiceCollectionExtensions
{
    /// <summary>
    /// Registers the audio-capture service. <see cref="WasapiAudioCaptureService.StartAsync"/> still throws
    /// <see cref="NotSupportedException"/> until the WASAPI pipeline exists (Phase 3).
    /// </summary>
    public static IServiceCollection AddKingRecorderAudio(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<WasapiAudioCaptureService>();
        services.TryAddSingleton<IAudioCaptureService>(static provider => provider.GetRequiredService<WasapiAudioCaptureService>());

        return services;
    }
}
