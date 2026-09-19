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
    ///     The configured provider API key.
    /// </summary>
    public const string ApiKeyKey = SectionKey + ":ApiKey";

    /// <summary>
    ///     The configured request timeout.
    /// </summary>
    public const string RequestTimeoutKey = SectionKey + ":RequestTimeout";
}
