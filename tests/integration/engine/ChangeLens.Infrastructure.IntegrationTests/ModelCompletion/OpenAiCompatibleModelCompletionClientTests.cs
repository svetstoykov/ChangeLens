using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Core.ModelCompletion.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;
using ChangeLens.Infrastructure.ModelCompletion.Constants;
using ChangeLens.Infrastructure.ModelCompletion.Extensions;
using ChangeLens.Infrastructure.ModelCompletion.Models;
using ChangeLens.Infrastructure.ModelCompletion.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;
using ModelCompletionModel = ChangeLens.Core.ModelCompletion.Models.ModelCompletion;

namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion;

/// <summary>
///     Verifies the OpenAI-compatible completion adapter through real loopback HTTP sockets.
/// </summary>
public sealed class OpenAiCompatibleModelCompletionClientTests
{
    private const string ApiKey = "fixture-secret-key";
    private const string SystemMessage = "system source quote";
    private const string UserMessage = "user source quote";

    /// <summary>
    ///     Asynchronously sends the endpoint, authorization, JSON response format, and token settings expected by the provider.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_RequestContainsProviderContractWithoutTools()
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            static (_, _) => Task.FromResult(SuccessResponse("{\"ok\":true}")));
        using var httpClient = new HttpClient();
        var client = CreateClient(
            httpClient,
            new ModelCompletionOptions
            {
                BaseUrl = new Uri(server.BaseAddress, "v1").ToString(),
                Model = "fixture-model",
                ApiKey = ApiKey,
            });

        var result = await client.CompleteJsonAsync(
            new ModelCompletionRequest(SystemMessage, UserMessage, 321, ModelReasoningEffort.High),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(server.Requests);
        Assert.Equal("POST", request.Method);
        Assert.Equal("/v1/chat/completions", request.Path);
        Assert.Equal($"Bearer {ApiKey}", request.Headers["Authorization"]);
        using var body = JsonDocument.Parse(request.Body);
        var root = body.RootElement;
        Assert.Equal("fixture-model", root.GetProperty("model").GetString());
        Assert.Equal(321, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal(0, root.GetProperty("temperature").GetDouble());
        Assert.Equal("json_object", root.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Equal("high", root.GetProperty("reasoning_effort").GetString());
        Assert.False(root.TryGetProperty("tools", out _));
        var messages = root.GetProperty("messages").EnumerateArray().ToArray();
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal(SystemMessage, messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal(UserMessage, messages[1].GetProperty("content").GetString());
    }

    /// <summary>
    ///     Asynchronously maps the provider model, response text, latency, and every supported usage count.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_ReplyWithUsageMapsCompletionAndCounts()
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            static (_, _) => Task.FromResult(SuccessResponseWithUsage("{\"answer\":1}")));
        using var httpClient = new HttpClient();
        var client = CreateClient(httpClient, CreateOptions(server));

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var completion = Assert.IsType<ModelCompletionModel>(result.Data);
        Assert.Equal("provider-model", completion.Model);
        Assert.Equal("{\"answer\":1}", completion.Text);
        Assert.True(completion.LatencyMilliseconds >= 0);
        Assert.Equal(17, completion.InputTokens);
        Assert.Equal(8, completion.OutputTokens);
        Assert.Equal(4, completion.CachedInputTokens);
        Assert.Equal(3, completion.ReasoningTokens);
    }

    /// <summary>
    ///     Asynchronously preserves null usage measurements when the provider omits the usage block.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_ReplyWithoutUsagePreservesNullCounts()
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            static (_, _) => Task.FromResult(SuccessResponse("{\"answer\":1}")));
        using var httpClient = new HttpClient();
        var client = CreateClient(httpClient, CreateOptions(server));

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var completion = Assert.IsType<ModelCompletionModel>(result.Data);
        Assert.Null(completion.InputTokens);
        Assert.Null(completion.OutputTokens);
        Assert.Null(completion.CachedInputTokens);
        Assert.Null(completion.ReasoningTokens);
    }

    /// <summary>
    ///     Asynchronously maps provider HTTP statuses to their stable transport-independent errors.
    /// </summary>
    /// <param name="statusCode">The provider response status.</param>
    /// <param name="errorType">The expected broad error category.</param>
    /// <param name="errorCode">The expected stable error code.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Theory]
    [InlineData(400, ErrorType.ExternalDependencyFailure, "modelCompletion.requestFailed")]
    [InlineData(401, ErrorType.Unauthorized, "modelCompletion.unauthorized")]
    [InlineData(403, ErrorType.Unauthorized, "modelCompletion.unauthorized")]
    [InlineData(404, ErrorType.ExternalDependencyFailure, "modelCompletion.requestFailed")]
    [InlineData(429, ErrorType.ExternalDependencyFailure, "modelCompletion.rateLimited")]
    [InlineData(500, ErrorType.ExternalDependencyFailure, "modelCompletion.providerUnavailable")]
    [InlineData(503, ErrorType.ExternalDependencyFailure, "modelCompletion.providerUnavailable")]
    public async Task CompleteJsonAsync_HttpFailureMapsStatus(
        int statusCode,
        ErrorType errorType,
        string errorCode)
    {
        const string providerBody = "provider body must stay out of the error";
        await using var server = await LoopbackHttpServer.StartAsync(
            (_, _) => Task.FromResult(new LoopbackHttpResponse(statusCode, providerBody)));
        using var httpClient = new HttpClient();
        var client = CreateClient(httpClient, CreateOptions(server));

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        AssertFailure(result, errorType, errorCode);
        Assert.DoesNotContain(providerBody, Assert.Single(result.Errors).Message, StringComparison.Ordinal);
        Assert.Equal(1, server.RequestCount);
    }

    /// <summary>
    ///     Asynchronously rejects malformed completion envelopes and empty content.
    /// </summary>
    /// <param name="responseBody">The malformed provider response.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Theory]
    [InlineData("not json")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":null}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"\"}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"   \"}}]}")]
    public async Task CompleteJsonAsync_MalformedEnvelopeReturnsStableError(string responseBody)
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            (_, _) => Task.FromResult(SuccessResponseBody(responseBody)));
        using var httpClient = new HttpClient();
        var client = CreateClient(httpClient, CreateOptions(server));

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        AssertFailure(result, ErrorType.ExternalDependencyFailure, "modelCompletion.malformedResponse");
    }

    /// <summary>
    ///     Asynchronously forwards non-empty provider content to the caller even when that content is not valid JSON.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_NonEmptyInvalidJsonContentSucceeds()
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            static (_, _) => Task.FromResult(SuccessResponse("not-json")));
        using var httpClient = new HttpClient();
        var client = CreateClient(httpClient, CreateOptions(server));

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("not-json", Assert.IsType<ModelCompletionModel>(result.Data).Text);
    }

    /// <summary>
    ///     Asynchronously returns a timeout result when the provider exceeds the configured request deadline.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_ProviderDelayReturnsTimeoutError()
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            async (_, _) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300));
                return SuccessResponse("{\"ok\":true}");
            });
        using var httpClient = new HttpClient();
        var options = CreateOptions(server);
        options.RequestTimeout = TimeSpan.FromMilliseconds(30);
        var client = CreateClient(httpClient, options);

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        AssertFailure(result, ErrorType.Timeout, "modelCompletion.timeout");
        Assert.Equal(1, server.RequestCount);
    }

    /// <summary>
    ///     Asynchronously preserves caller cancellation as an exception instead of converting it to a timeout result.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_CallerCancellationThrowsCancellationException()
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            async (_, _) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300));
                return SuccessResponse("{\"ok\":true}");
            });
        using var httpClient = new HttpClient();
        var options = CreateOptions(server);
        options.RequestTimeout = TimeSpan.FromSeconds(2);
        var client = CreateClient(httpClient, options);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CompleteJsonAsync(CreateRequest(), cancellation.Token));
        Assert.Equal(1, server.RequestCount);
    }

    /// <summary>
    ///     Asynchronously records caller cancellation without converting it to a provider error.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_CallerCancellationLogsSanitizedOutcome()
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            async (_, _) =>
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300));
                return SuccessResponse("{\"ok\":true}");
            });
        using var httpClient = new HttpClient();
        var logger = new RecordingModelCompletionLogger<OpenAiCompatibleModelCompletionClient>();
        var client = CreateClient(httpClient, CreateOptions(server), logger);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.CompleteJsonAsync(CreateRequest(), cancellation.Token));

        Assert.Contains(logger.Entries, entry => entry.Message.Contains("canceled", StringComparison.Ordinal));
        Assert.All(logger.Entries, entry => Assert.DoesNotContain(ApiKey, entry.Message, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Asynchronously maps a socket failure to provider-unavailable without exposing transport details.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_SocketFailureReturnsProviderUnavailable()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var baseUrl = new Uri($"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/");
        listener.Stop();
        using var httpClient = new HttpClient();
        var client = CreateClient(
            httpClient,
            new ModelCompletionOptions { BaseUrl = baseUrl.ToString(), Model = "fixture-model", ApiKey = ApiKey });

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        AssertFailure(result, ErrorType.ExternalDependencyFailure, "modelCompletion.providerUnavailable");
    }

    /// <summary>
    ///     Asynchronously reports missing provider configuration at call time without requiring an HTTP request.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_MissingConfigurationReturnsCallTimeError()
    {
        using var httpClient = new HttpClient();
        var client = CreateClient(httpClient, new ModelCompletionOptions());

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        AssertFailure(result, ErrorType.InvalidOperation, "modelCompletion.notConfigured");
    }

    /// <summary>
    ///     Asynchronously rejects an invalid bearer key as call-time configuration without exposing the key.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_InvalidBearerKeyReturnsSafeConfigurationError()
    {
        await using var server = await LoopbackHttpServer.StartAsync(
            static (_, _) => Task.FromResult(SuccessResponse("{\"ok\":true}")));
        const string invalidApiKey = "fixture-secret\r\nX-Leaked: true";
        using var httpClient = new HttpClient();
        var client = CreateClient(
            httpClient,
            new ModelCompletionOptions
            {
                BaseUrl = server.BaseAddress.ToString(),
                Model = "fixture-model",
                ApiKey = invalidApiKey,
            });

        var result = await client.CompleteJsonAsync(CreateRequest(), CancellationToken.None);

        AssertFailure(result, ErrorType.InvalidOperation, "modelCompletion.notConfigured");
        Assert.DoesNotContain(invalidApiKey, Assert.Single(result.Errors).Message, StringComparison.Ordinal);
        Assert.Equal(0, server.RequestCount);
    }

    /// <summary>
    ///     Asynchronously keeps request and provider content out of adapter log entries.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_LogsOutcomeWithoutSecretsOrBodies()
    {
        const string responseSecret = "provider-response-source-secret";
        await using var server = await LoopbackHttpServer.StartAsync(
            (_, _) => Task.FromResult(SuccessResponse(responseSecret)));
        using var httpClient = new HttpClient();
        var logger = new RecordingModelCompletionLogger<OpenAiCompatibleModelCompletionClient>();
        var client = CreateClient(httpClient, CreateOptions(server), logger);

        var result = await client.CompleteJsonAsync(
            new ModelCompletionRequest(SystemMessage, UserMessage, 128),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(logger.Entries, entry => entry.Message.Contains("success", StringComparison.Ordinal));
        Assert.All(
            logger.Entries,
            entry =>
            {
                Assert.DoesNotContain(ApiKey, entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(SystemMessage, entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(UserMessage, entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(responseSecret, entry.Message, StringComparison.Ordinal);
            });
    }

    /// <summary>
    ///     Asynchronously keeps secrets and bodies out of the default typed-client logging pipeline.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task CompleteJsonAsync_DefaultHttpClientLoggingDoesNotExposeSecretsOrBodies()
    {
        const string responseSecret = "provider-response-source-secret";
        await using var server = await LoopbackHttpServer.StartAsync(
            (_, _) => Task.FromResult(SuccessResponse(responseSecret)));
        using var loggerProvider = new RecordingModelCompletionLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(loggerProvider));
        services.Configure<ModelCompletionOptions>(options =>
        {
            options.BaseUrl = server.BaseAddress.ToString();
            options.Model = "fixture-model";
            options.ApiKey = ApiKey;
        });
        services.AddModelCompletionClient();
        using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<IModelCompletionClient>();

        var result = await client.CompleteJsonAsync(
            new ModelCompletionRequest(SystemMessage, UserMessage, 128),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(loggerProvider.Entries);
        Assert.All(
            loggerProvider.Entries,
            entry =>
            {
                Assert.DoesNotContain(ApiKey, entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(SystemMessage, entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(UserMessage, entry.Message, StringComparison.Ordinal);
                Assert.DoesNotContain(responseSecret, entry.Message, StringComparison.Ordinal);
            });
    }

    private static ModelCompletionRequest CreateRequest() =>
        new(SystemMessage, UserMessage, 128, ModelReasoningEffort.Low);

    private static ModelCompletionOptions CreateOptions(LoopbackHttpServer server) =>
        new()
        {
            BaseUrl = server.BaseAddress.ToString(),
            Model = "fixture-model",
            ApiKey = ApiKey,
        };

    private static OpenAiCompatibleModelCompletionClient CreateClient(
        HttpClient httpClient,
        ModelCompletionOptions options,
        RecordingModelCompletionLogger<OpenAiCompatibleModelCompletionClient>? logger = null) =>
        new(httpClient, Options.Create(options),
            logger is null ? NullLogger<OpenAiCompatibleModelCompletionClient>.Instance : logger);

    private static LoopbackHttpResponse SuccessResponse(string completionText) =>
        SuccessResponseBody(
            $"{{\"model\":\"provider-model\",\"choices\":[{{\"message\":{{\"content\":{JsonSerializer.Serialize(completionText)}}}}}]}}");

    private static LoopbackHttpResponse SuccessResponseWithUsage(string completionText) =>
        SuccessResponseBody(
            $"{{\"model\":\"provider-model\",\"choices\":[{{\"message\":{{\"content\":{JsonSerializer.Serialize(completionText)}}}}}],"
            + "\"usage\":{\"prompt_tokens\":17,\"completion_tokens\":8,\"prompt_tokens_details\":{\"cached_tokens\":4},"
            + "\"completion_tokens_details\":{\"reasoning_tokens\":3}}}");

    private static LoopbackHttpResponse SuccessResponseBody(string body) => new(200, body);

    private static void AssertFailure(Result<ModelCompletionModel> result, ErrorType errorType, string errorCode)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(errorType, error.Type);
        Assert.Equal(errorCode, error.Code);
        Assert.Null(result.Data);
    }
}
