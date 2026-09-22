using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ChangeLens.Core.ModelCompletion.Constants;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.ModelCompletion.Constants;
using ChangeLens.Infrastructure.ModelCompletion.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Infrastructure.ModelCompletion.Services;

/// <summary>
///     Sends one JSON-only completion to an OpenAI-compatible chat-completions endpoint.
/// </summary>
/// <remarks>
///     The adapter performs one HTTP attempt per call. It does not expose provider tools or retry failed requests.
///     Configuration is checked when a call is made so an engine without provider credentials can still start.
/// </remarks>
public sealed class OpenAiCompatibleModelCompletionClient : IModelCompletionClient
{
    private static readonly JsonSerializerOptions RequestSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ModelCompletionOptions _options;
    private readonly ILogger<OpenAiCompatibleModelCompletionClient> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OpenAiCompatibleModelCompletionClient" /> class.
    /// </summary>
    /// <param name="httpClient">The typed HTTP client. Cannot be <see langword="null" />.</param>
    /// <param name="options">The provider settings. Cannot be <see langword="null" />.</param>
    /// <param name="logger">The adapter logger. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="httpClient" />, <paramref name="options" />, or <paramref name="logger" /> is
    ///     <see langword="null" />.
    /// </exception>
    public OpenAiCompatibleModelCompletionClient(
        HttpClient httpClient,
        IOptions<ModelCompletionOptions> options,
        ILogger<OpenAiCompatibleModelCompletionClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options.Value);

        this._httpClient = httpClient;
        this._httpClient.Timeout = Timeout.InfiniteTimeSpan;
        this._options = options.Value;
        this._logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<ModelCompletionModel>> CompleteJsonAsync(
        ModelCompletionRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var startedAt = Stopwatch.GetTimestamp();
        var model = this._options.Model?.Trim();
        this._logger.LogInformation(
            "Starting model completion attempt for model {Model} with maximum output tokens {MaximumOutputTokens}.",
            model,
            request.MaximumOutputTokens);

        if (!TryCreateEndpoint(this._options.BaseUrl, out var endpoint)
            || string.IsNullOrWhiteSpace(model)
            || !TryCreateAuthorizationHeader(this._options.ApiKey, out var authorization)
            || this._options.RequestTimeout <= TimeSpan.Zero)
        {
            return this.FinishFailure(
                OperationError.InvalidOperation(
                    "Model completion is not configured.",
                    ModelCompletionErrorCode.NotConfigured),
                startedAt,
                null,
                "notConfigured");
        }

        var serializedRequest = JsonSerializer.Serialize(
            new OpenAiCompatibleChatCompletionRequest(
                model,
                [
                    new OpenAiCompatibleChatMessage("system", request.SystemMessage),
                    new OpenAiCompatibleChatMessage("user", request.UserMessage),
                ],
                request.MaximumOutputTokens,
                new OpenAiCompatibleResponseFormat("json_object"),
                request.ReasoningEffort is null ? 0 : null,
                request.ReasoningEffort?.ToString().ToLowerInvariant()),
            RequestSerializerOptions);

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(serializedRequest, Encoding.UTF8, "application/json"),
        };
        httpRequest.Headers.Authorization = authorization;

        using var timeoutCancellation = new CancellationTokenSource(this._options.RequestTimeout);
        using var requestCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCancellation.Token);

        try
        {
            using var response = await this._httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                requestCancellation.Token);

            if (!response.IsSuccessStatusCode)
            {
                var error = await this.ReadHttpErrorAsync(response, requestCancellation.Token, cancellationToken);
                return this.FinishFailure(error, startedAt, (int)response.StatusCode, ErrorOutcome(error));
            }

            var responseBody = await this.ReadBoundedResponseBodyAsync(response.Content, requestCancellation.Token);
            var providerError = TryParseProviderError(responseBody, response.StatusCode);
            if (providerError is not null)
            {
                return this.FinishFailure(providerError, startedAt, (int)response.StatusCode, ErrorOutcome(providerError));
            }

            if (responseBody is null || !TryParseCompletion(responseBody, model, out var completion))
            {
                return this.FinishFailure(
                    OperationError.ExternalDependencyFailure(
                        "The model completion provider returned a malformed response.",
                        ModelCompletionErrorCode.MalformedResponse),
                    startedAt,
                    (int)response.StatusCode,
                    "malformedResponse");
            }

            var result = Result.Success(new ModelCompletionModel(
                completion.Model,
                completion.Text,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
                completion.InputTokens,
                completion.OutputTokens,
                completion.CachedInputTokens,
                completion.ReasoningTokens));
            this.LogOutcome(
                startedAt,
                "success",
                null,
                (int)response.StatusCode,
                result.Data!.InputTokens,
                result.Data.OutputTokens,
                result.Data.CachedInputTokens,
                result.Data.ReasoningTokens);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            this.LogOutcome(startedAt, "canceled", null, null, null, null, null, null);
            throw;
        }
        catch (OperationCanceledException) when (timeoutCancellation.IsCancellationRequested)
        {
            return this.FinishFailure(
                OperationError.Timeout(
                    "The model completion provider exceeded its allowed time.",
                    ModelCompletionErrorCode.Timeout),
                startedAt,
                null,
                "timeout");
        }
        catch (HttpRequestException)
        {
            return this.FinishFailure(
                OperationError.ExternalDependencyFailure(
                    "The model completion provider is unavailable.",
                    ModelCompletionErrorCode.ProviderUnavailable),
                startedAt,
                null,
                "providerUnavailable");
        }
    }

    /// <summary>
    ///     Asynchronously reads a provider body up to the configured byte budget and optional lower limit.
    /// </summary>
    /// <param name="content">The HTTP response content. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">The token used to cancel the read.</param>
    /// <param name="byteLimit">An additional byte limit, or <see langword="null" /> for the configured budget.</param>
    /// <returns>
    ///     A task whose result is the UTF-8 body, or <see langword="null" /> when the body exceeds the budget.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="content" /> is <see langword="null" />.</exception>
    private async Task<string?> ReadBoundedResponseBodyAsync(HttpContent content, CancellationToken cancellationToken, int? byteLimit = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        var maximumBytes = this._options.MaximumResponseBytes > 0
            ? this._options.MaximumResponseBytes
            : ModelCompletionTransportConstants.ResponseByteBudget(ModelCompletionTransportConstants.DefaultMaximumOutputCharacters);
        maximumBytes = Math.Min(maximumBytes, byteLimit ?? maximumBytes);
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        var total = 0;
        while (true)
        {
            var read = await stream.ReadAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, (long)maximumBytes - total + 1)), cancellationToken);
            if (read == 0)
            {
                break;
            }

            total += read;
            if (total > maximumBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    // Error bodies are optional diagnostics: a failed read must not hide the known HTTP failure or caller cancellation.
    private async Task<OperationError> ReadHttpErrorAsync(
        HttpResponseMessage response, CancellationToken requestCancellation, CancellationToken callerCancellation)
    {
        try
        {
            var body = await this.ReadBoundedResponseBodyAsync(response.Content, requestCancellation, ProviderErrorConstants.MaximumBodyBytes);
            return TryParseProviderError(body, response.StatusCode) ?? CreateHttpError(response.StatusCode);
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or OperationCanceledException)
        {
            callerCancellation.ThrowIfCancellationRequested();
            return CreateHttpError(response.StatusCode);
        }
    }

    // Only recognized fields become diagnostics. Arbitrary provider prose and metadata may echo source text or credentials.
    private static OperationError? TryParseProviderError(string? body, HttpStatusCode httpStatus)
    {
        if (body is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (root.TryGetProperty(ProviderErrorConstants.Error, out var error) && error.ValueKind is not JsonValueKind.Null)
            {
                return CreateProviderError(error, httpStatus);
            }

            if (root.TryGetProperty(ProviderErrorConstants.Choices, out var choices)
                && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.ValueKind == JsonValueKind.Object)
                {
                    if (choice.TryGetProperty(ProviderErrorConstants.Error, out error) && error.ValueKind is not JsonValueKind.Null)
                    {
                        return CreateProviderError(error, httpStatus);
                    }

                    if (ReadString(choice, ProviderErrorConstants.FinishReason) == ProviderErrorConstants.Error)
                    {
                        return CreateProviderError(default, httpStatus);
                    }
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static OperationError CreateProviderError(JsonElement error, HttpStatusCode httpStatus)
    {
        var code = ReadString(error, ProviderErrorConstants.Code);
        var numericCode = ReadNullableInt(error, ProviderErrorConstants.Code);
        if (numericCode is null && int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedCode))
        {
            numericCode = parsedCode;
        }

        numericCode = numericCode is >= 400 and <= 599 ? numericCode : null;
        var knownCode = RecognizeProviderCode(code);
        var errorType = error.ValueKind == JsonValueKind.Object && error.TryGetProperty(ProviderErrorConstants.Metadata, out var metadata)
            ? RecognizeProviderCode(ReadString(metadata, ProviderErrorConstants.ErrorType))
            : null;
        var providerStatus = numericCode ?? StatusForProviderCode(knownCode) ?? StatusForProviderCode(errorType);
        // A failed HTTP status remains authoritative; embedded codes classify errors inside successful HTTP envelopes.
        var effectiveStatus = (int)httpStatus >= 400 ? httpStatus : (HttpStatusCode)(providerStatus ?? 400);
        var mapped = CreateHttpError(effectiveStatus);
        var detail = "The provider reported an error.";
        var message = ReadString(error, ProviderErrorConstants.Message);
        if (knownCode == ProviderErrorConstants.ModelNotFound
            || message?.EndsWith(" is not a valid model ID", StringComparison.Ordinal) == true)
        {
            detail = "The provider rejected an invalid model ID. Check the configured model identifier.";
        }
        else if (message == "JSON error injected into SSE stream")
        {
            detail = "JSON error injected into SSE stream.";
        }

        var providerCode = numericCode?.ToString(CultureInfo.InvariantCulture) ?? knownCode ?? "unrecognized";
        var retry = effectiveStatus is HttpStatusCode.TooManyRequests or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout
            ? " A retry may succeed; no automatic retry was attempted."
            : string.Empty;
        var summary = (int)httpStatus < 400 && mapped.Code == ModelCompletionErrorCode.RequestFailed
            ? "The model completion provider reported a failure."
            : mapped.Message;
        var diagnostic = $"{summary} {detail} Provider code: {providerCode}; type: {errorType ?? "unrecognized"}.{retry}";
        return new OperationError(diagnostic, mapped.Type, mapped.Code);
    }

    private static string? ReadString(JsonElement parent, string name) =>
        parent.ValueKind == JsonValueKind.Object && parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? RecognizeProviderCode(string? code) => code switch
    {
        ProviderErrorConstants.InvalidApiKey or ProviderErrorConstants.RateLimitExceeded or ProviderErrorConstants.ProviderUnavailable
            or ProviderErrorConstants.ModelNotFound or ProviderErrorConstants.InvalidRequestError => code,
        _ => null,
    };

    private static int? StatusForProviderCode(string? code) => code switch
    {
        ProviderErrorConstants.InvalidApiKey => 401,
        ProviderErrorConstants.RateLimitExceeded => 429,
        ProviderErrorConstants.ProviderUnavailable => 503,
        ProviderErrorConstants.ModelNotFound => 404,
        ProviderErrorConstants.InvalidRequestError => 400,
        _ => null,
    };

    private static bool TryCreateEndpoint(string? baseUrl, out Uri endpoint)
    {
        endpoint = null!;
        if (string.IsNullOrWhiteSpace(baseUrl)
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(baseUri.UserInfo))
        {
            return false;
        }

        var builder = new UriBuilder(baseUri)
        {
            Path = baseUri.AbsolutePath.TrimEnd('/') + "/chat/completions",
            Query = string.Empty,
            Fragment = string.Empty,
        };
        endpoint = builder.Uri;
        return true;
    }

    private static bool TryCreateAuthorizationHeader(string? apiKey, out AuthenticationHeaderValue authorization)
    {
        authorization = null!;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        if (!AuthenticationHeaderValue.TryParse($"Bearer {apiKey.Trim()}", out var parsedAuthorization)
            || parsedAuthorization is null
            || !string.Equals(parsedAuthorization.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        authorization = parsedAuthorization;
        return true;
    }

    private static OperationError CreateHttpError(HttpStatusCode statusCode)
    {
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return OperationError.Unauthorized(
                "The model completion provider rejected the configured credentials.",
                ModelCompletionErrorCode.Unauthorized);
        }

        if (statusCode == (HttpStatusCode)429)
        {
            return OperationError.ExternalDependencyFailure(
                "The model completion provider rate limit was reached.",
                ModelCompletionErrorCode.RateLimited);
        }

        return statusCode >= HttpStatusCode.InternalServerError
            ? OperationError.ExternalDependencyFailure(
                "The model completion provider is unavailable.",
                ModelCompletionErrorCode.ProviderUnavailable)
            : OperationError.ExternalDependencyFailure(
                $"The model completion request failed with HTTP status {(int)statusCode}.",
                ModelCompletionErrorCode.RequestFailed);
    }

    private static string ErrorOutcome(OperationError error) => error.Code switch
    {
        ModelCompletionErrorCode.Unauthorized => "unauthorized",
        ModelCompletionErrorCode.RateLimited => "rateLimited",
        ModelCompletionErrorCode.ProviderUnavailable => "providerUnavailable",
        _ => "requestFailed",
    };

    private static bool TryParseCompletion(string responseBody, string configuredModel, out ParsedModelCompletion completion)
    {
        completion = null!;
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return false;
            }

            var firstChoice = choices[0];
            if (firstChoice.ValueKind != JsonValueKind.Object
                || !firstChoice.TryGetProperty("message", out var message)
                || message.ValueKind != JsonValueKind.Object
                || !message.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var text = content.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var model = root.TryGetProperty("model", out var modelElement)
                && modelElement.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(modelElement.GetString())
                ? modelElement.GetString()!
                : configuredModel;
            var usage = root.TryGetProperty("usage", out var usageElement)
                && usageElement.ValueKind == JsonValueKind.Object
                ? usageElement
                : (JsonElement?)null;
            var inputTokens = ReadNullableInt(usage, "prompt_tokens");
            var outputTokens = ReadNullableInt(usage, "completion_tokens");
            var cachedInputTokens = ReadNestedNullableInt(usage, "prompt_tokens_details", "cached_tokens");
            var reasoningTokens = ReadNestedNullableInt(usage, "completion_tokens_details", "reasoning_tokens");
            completion = new ParsedModelCompletion(
                model,
                text,
                inputTokens,
                outputTokens,
                cachedInputTokens,
                reasoningTokens);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int? ReadNullableInt(JsonElement? parent, string propertyName)
    {
        if (parent is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.Number
            || !property.TryGetInt32(out var result))
        {
            return null;
        }

        return result;
    }

    private static int? ReadNestedNullableInt(JsonElement? parent, string objectName, string propertyName)
    {
        if (parent is not { ValueKind: JsonValueKind.Object } value
            || !value.TryGetProperty(objectName, out var child)
            || child.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ReadNullableInt(child, propertyName);
    }

    private Result<ModelCompletionModel> FinishFailure(
        OperationError error,
        long startedAt,
        int? statusCode,
        string outcome)
    {
        this.LogOutcome(startedAt, outcome, error.Code, statusCode, null, null, null, null, error.Message);
        return Result.Fail<ModelCompletionModel>(error);
    }

    private void LogOutcome(
        long startedAt,
        string outcome,
        string? errorCode,
        int? statusCode,
        int? inputTokens,
        int? outputTokens,
        int? cachedInputTokens,
        int? reasoningTokens,
        string? failureDetail = null)
    {
        this._logger.LogInformation(
            "Model completion attempt finished with outcome {Outcome}, error code {ErrorCode}, HTTP status {StatusCode}, "
            + "elapsed {ElapsedMilliseconds:0.000} ms, input tokens {InputTokens}, output tokens {OutputTokens}, "
            + "cached input tokens {CachedInputTokens}, reasoning tokens {ReasoningTokens}, failure detail {FailureDetail}.",
            outcome,
            errorCode,
            statusCode,
            Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds,
            inputTokens,
            outputTokens,
            cachedInputTokens,
            reasoningTokens,
            failureDetail);
    }

}
