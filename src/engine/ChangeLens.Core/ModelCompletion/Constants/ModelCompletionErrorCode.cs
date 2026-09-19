namespace ChangeLens.Core.ModelCompletion.Constants;

/// <summary>
///     Provides stable error codes for model completion provider calls.
/// </summary>
public static class ModelCompletionErrorCode
{
    /// <summary>Identifies a completion provider that is missing required configuration.</summary>
    public const string NotConfigured = "modelCompletion.notConfigured";

    /// <summary>Identifies a provider that rejected the configured credentials.</summary>
    public const string Unauthorized = "modelCompletion.unauthorized";

    /// <summary>Identifies a provider rate-limit response.</summary>
    public const string RateLimited = "modelCompletion.rateLimited";

    /// <summary>Identifies a provider request that failed with an unclassified HTTP status.</summary>
    public const string RequestFailed = "modelCompletion.requestFailed";

    /// <summary>Identifies a provider or network failure that makes the completion unavailable.</summary>
    public const string ProviderUnavailable = "modelCompletion.providerUnavailable";

    /// <summary>Identifies a provider response that does not contain one usable completion.</summary>
    public const string MalformedResponse = "modelCompletion.malformedResponse";

    /// <summary>Identifies a provider request that exceeded its configured timeout.</summary>
    public const string Timeout = "modelCompletion.timeout";
}
