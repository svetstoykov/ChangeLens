namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;

/// <summary>
///     Represents one response returned by the loopback provider.
/// </summary>
public sealed record LoopbackHttpResponse(int StatusCode, string Body, string ContentType = "application/json");
