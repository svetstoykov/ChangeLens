namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;

/// <summary>
///     Fails every outgoing request with a fixed exception, for transport failures a loopback server cannot produce.
/// </summary>
/// <param name="exception">The exception every send fails with.</param>
public sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
{
    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(exception);
}
