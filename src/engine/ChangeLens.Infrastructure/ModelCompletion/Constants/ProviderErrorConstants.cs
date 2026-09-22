namespace ChangeLens.Infrastructure.ModelCompletion.Constants;

/// <summary>Defines bounded provider error fields and recognized diagnostic identifiers.</summary>
internal static class ProviderErrorConstants
{
    /// <summary>The maximum bytes read from an unsuccessful HTTP response.</summary>
    internal const int MaximumBodyBytes = 16 * 1024;

    /// <summary>The provider error member and failed finish reason.</summary>
    internal const string Error = "error";

    /// <summary>The provider code member.</summary>
    internal const string Code = "code";

    /// <summary>The provider message member.</summary>
    internal const string Message = "message";

    /// <summary>The provider metadata member.</summary>
    internal const string Metadata = "metadata";

    /// <summary>The provider error type member.</summary>
    internal const string ErrorType = "error_type";

    /// <summary>The completion choices member.</summary>
    internal const string Choices = "choices";

    /// <summary>The completion finish reason member.</summary>
    internal const string FinishReason = "finish_reason";

    /// <summary>The provider unavailable identifier.</summary>
    internal const string ProviderUnavailable = "provider_unavailable";

    /// <summary>The invalid credential identifier.</summary>
    internal const string InvalidApiKey = "invalid_api_key";

    /// <summary>The rate limit identifier.</summary>
    internal const string RateLimitExceeded = "rate_limit_exceeded";

    /// <summary>The invalid model identifier.</summary>
    internal const string ModelNotFound = "model_not_found";

    /// <summary>The invalid request identifier.</summary>
    internal const string InvalidRequestError = "invalid_request_error";
}
