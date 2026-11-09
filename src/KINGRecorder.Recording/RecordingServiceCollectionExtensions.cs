using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KINGRecorder.Core.Interfaces;
using KINGRecorder.Recording.Stubs;

namespace KINGRecorder.Recording;

/// <summary>
/// Composition-root helper for the recording layer. Kept free of WinUI types so it can be exercised by tests.
/// </summary>
public static class RecordingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the recording engine together with the explicit "unavailable" pipeline implementations.
    /// Those stubs throw <see cref="NotSupportedException"/> on use; they never fabricate recordings.
    /// </summary>
    public static IServiceCollection AddKingRecorderRecording(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IVideoCompositor, UnavailableVideoCompositor>();
        services.TryAddSingleton<IEncoderService, UnavailableEncoderService>();
        services.TryAddSingleton<RecordingEngine>();
        services.TryAddSingleton<IRecordingEngine>(static provider => provider.GetRequiredService<RecordingEngine>());

        return services;
    }
}
