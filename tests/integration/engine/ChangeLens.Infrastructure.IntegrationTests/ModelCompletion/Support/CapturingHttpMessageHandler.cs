using System.Net;
using System.Text;

namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;

/// <summary>
///     Captures outgoing request bodies and answers each with a fixed completion, for hosts a loopback server cannot stand in for.
/// </summary>
public sealed class CapturingHttpMessageHandler : HttpMessageHandler
{
    private const string Reply = "{\"model\":\"provider-model\",\"choices\":[{\"message\":{\"content\":\"{}\"}}]}";

    private readonly List<(Uri Uri, string Body)> _requests = [];

    /// <summary>
    ///     Gets the captured request URIs and bodies in send order.
    /// </summary>
    public IReadOnlyList<(Uri Uri, string Body)> Requests => this._requests;

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
        this._requests.Add((request.RequestUri!, body));
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Reply, Encoding.UTF8, "application/json") };
    }
}
