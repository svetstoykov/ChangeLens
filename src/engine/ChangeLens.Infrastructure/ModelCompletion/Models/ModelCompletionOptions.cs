using ChangeLens.Infrastructure.ModelCompletion.Constants;

namespace ChangeLens.Infrastructure.ModelCompletion.Models;

/// <summary>
///     Holds the engine-only settings for one OpenAI-compatible model completion provider.
/// </summary>
public sealed class ModelCompletionOptions
{
    /// <summary>
    ///     Gets or sets the provider base URL, or <see langword="null" /> when no provider is configured.
    /// </summary>
    public string? BaseUrl { get; set; }

    /// <summary>
    ///     Gets or sets the provider model slug, or <see langword="null" /> when no provider is configured.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    ///     Gets or sets the provider API key, or <see langword="null" /> when no provider is configured.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    ///     Gets or sets the maximum time allowed for one provider request. The default is two minutes.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    ///     Gets or sets the maximum number of bytes read from a successful provider response body.
    /// </summary>
    /// <remarks>
    ///     The default covers the curator output-character cap with JSON-escape and envelope headroom.
    /// </remarks>
    public int MaximumResponseBytes { get; set; } =
        ModelCompletionTransportConstants.ResponseByteBudget(ModelCompletionTransportConstants.DefaultMaximumOutputCharacters);
}
