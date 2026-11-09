using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KINGRecorder.Infrastructure;

/// <summary>
/// Composition-root helper for the infrastructure layer. Kept free of WinUI types so it can be exercised by tests.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers settings persistence as a singleton (it is stateless apart from the file path).</summary>
    public static IServiceCollection AddKingRecorderInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<SettingsManager>();

        return services;
    }
}
