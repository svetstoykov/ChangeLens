namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;

/// <summary>
///     Represents the bounded HTTP request captured by the loopback provider.
/// </summary>
public sealed record LoopbackHttpRequest(
    string Method,
    string Path,
    IReadOnlyDictionary<string, string> Headers,
    string Body);
