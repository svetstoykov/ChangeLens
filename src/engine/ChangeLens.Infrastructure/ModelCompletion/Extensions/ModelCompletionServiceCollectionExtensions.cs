using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Infrastructure.ModelCompletion.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Infrastructure.ModelCompletion.Extensions;

/// <summary>
///     Registers the OpenAI-compatible model completion adapter.
/// </summary>
public static class ModelCompletionServiceCollectionExtensions
{
    /// <summary>
    ///     Adds a typed HTTP client and the scoped provider-neutral completion port.
    /// </summary>
    /// <param name="services">The service collection to configure. Cannot be <see langword="null" />.</param>
    /// <returns>The supplied service collection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddModelCompletionClient(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient<OpenAiCompatibleModelCompletionClient>();
        services.AddScoped<IModelCompletionClient>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenAiCompatibleModelCompletionClient>());
        return services;
    }
}
