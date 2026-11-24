namespace ChangeLens.Infrastructure.ModelCompletion.Constants;

/// <summary>
///     Defines configuration keys for the OpenAI-compatible model completion adapter.
/// </summary>
public static class ModelCompletionConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing model completion settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:ModelCompletion";

    /// <summary>
    ///     The configured provider base URL.
    /// </summary>
    public const string BaseUrlKey = SectionKey + ":BaseUrl";

    /// <summary>
    ///     The configured provider model slug.
    /// </summary>
    public const string ModelKey = SectionKey + ":Model";

    /// <summary>
    ///     The configured path of the local key file that holds the provider API key.
    /// </summary>
    public const string ApiKeyFileKey = SectionKey + ":ApiKeyFile";

    /// <summary>
    ///     The name of the key file entry whose value is the provider API key.
    /// </summary>
    public const string ApiKeyEntryName = "CHANGELENS_API_KEY";

    /// <summary>
    ///     The configured request timeout.
    /// </summary>
    public const string RequestTimeoutKey = SectionKey + ":RequestTimeout";

    /// <summary>
    ///     The OpenRouter upstream ordering: <c>price</c>, <c>throughput</c>, or <c>latency</c>. Set it only for OpenRouter.
    /// </summary>
    public const string OpenRouterSortKey = SectionKey + ":OpenRouterSort";

    /// <summary>
    ///     The comma-separated OpenRouter upstream names never to route to. Set it only for OpenRouter.
    /// </summary>
    public const string OpenRouterIgnoreKey = SectionKey + ":OpenRouterIgnore";
}
