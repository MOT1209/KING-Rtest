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
    /// Registers the audio-capture service. WASAPI loopback + microphone capture are implemented (Phase 3);
    /// the service raises <see cref="IAudioCaptureService.AudioDataAvailable"/> with gain-applied PCM buffers.
    /// </summary>
    public static IServiceCollection AddKingRecorderAudio(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<WasapiAudioCaptureService>();
        services.TryAddSingleton<IAudioCaptureService>(static provider => provider.GetRequiredService<WasapiAudioCaptureService>());

        return services;
    }
}
