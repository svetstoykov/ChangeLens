namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;

/// <summary>
///     Represents one response returned by the loopback provider. A <paramref name="DeclaredContentLength" /> larger than the
///     body makes the server close the connection before the declared length arrives.
/// </summary>
public sealed record LoopbackHttpResponse(
    int StatusCode, string Body, string ContentType = "application/json", int? DeclaredContentLength = null);
