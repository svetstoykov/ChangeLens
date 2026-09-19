using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ChangeLens.Infrastructure.IntegrationTests.ModelCompletion.Support;

/// <summary>
///     Serves controlled HTTP responses over a real loopback TCP socket for adapter integration tests.
/// </summary>
public sealed class LoopbackHttpServer : IAsyncDisposable
{
    private const int MaximumHeaderBytes = 64 * 1024;
    private const int MaximumBodyBytes = 8 * 1024 * 1024;

    private readonly TcpListener _listener;
    private readonly Func<LoopbackHttpRequest, CancellationToken, Task<LoopbackHttpResponse>> _handler;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly ConcurrentQueue<LoopbackHttpRequest> _requests = new();
    private readonly Task _acceptLoop;

    private LoopbackHttpServer(Func<LoopbackHttpRequest, CancellationToken, Task<LoopbackHttpResponse>> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        this._handler = handler;
        this._listener = new TcpListener(IPAddress.Loopback, 0);
        this._listener.Start();
        this.BaseAddress = new Uri($"http://127.0.0.1:{((IPEndPoint)this._listener.LocalEndpoint).Port}/");
        this._acceptLoop = this.AcceptLoopAsync();
    }

    /// <summary>
    ///     Gets the server base address with a trailing slash.
    /// </summary>
    public Uri BaseAddress { get; }

    /// <summary>
    ///     Gets the number of requests accepted by the server.
    /// </summary>
    public int RequestCount => this._requests.Count;

    /// <summary>
    ///     Gets the requests accepted by the server in arrival order.
    /// </summary>
    public IReadOnlyList<LoopbackHttpRequest> Requests => this._requests.ToArray();

    /// <summary>
    ///     Starts a loopback server on an available local TCP port.
    /// </summary>
    /// <param name="handler">The response handler. Cannot be <see langword="null" />.</param>
    /// <returns>A task containing the started server.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="handler" /> is <see langword="null" />.</exception>
    public static Task<LoopbackHttpServer> StartAsync(
        Func<LoopbackHttpRequest, CancellationToken, Task<LoopbackHttpResponse>> handler)
    {
        return Task.FromResult(new LoopbackHttpServer(handler));
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        this._shutdown.Cancel();
        this._listener.Stop();
        try
        {
            await this._acceptLoop;
        }
        catch (OperationCanceledException) when (this._shutdown.IsCancellationRequested)
        {
        }
        finally
        {
            this._shutdown.Dispose();
        }
    }

    private async Task AcceptLoopAsync()
    {
        try
        {
            while (!this._shutdown.IsCancellationRequested)
            {
                using var client = await this._listener.AcceptTcpClientAsync(this._shutdown.Token);
                await this.HandleClientAsync(client);
            }
        }
        catch (OperationCanceledException) when (this._shutdown.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (this._shutdown.IsCancellationRequested)
        {
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using var stream = client.GetStream();
        var request = await ReadRequestAsync(stream, this._shutdown.Token);
        this._requests.Enqueue(request);
        var response = await this._handler(request, this._shutdown.Token);
        await WriteResponseAsync(stream, response, this._shutdown.Token);
    }

    private static async Task<LoopbackHttpRequest> ReadRequestAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var headerBytes = new MemoryStream();
        var oneByte = new byte[1];
        while (headerBytes.Length <= MaximumHeaderBytes)
        {
            var read = await stream.ReadAsync(oneByte.AsMemory(), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The loopback client closed the request before sending headers.");
            }

            headerBytes.WriteByte(oneByte[0]);
            if (headerBytes.Length >= 4 && EndsWithHeaderTerminator(headerBytes.GetBuffer(), (int)headerBytes.Length))
            {
                break;
            }
        }

        if (headerBytes.Length > MaximumHeaderBytes)
        {
            throw new InvalidDataException("The loopback request headers exceeded the test bound.");
        }

        var headerText = Encoding.ASCII.GetString(headerBytes.GetBuffer(), 0, (int)headerBytes.Length - 4);
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        var requestLine = lines[0].Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (requestLine.Length != 3)
        {
            throw new InvalidDataException("The loopback request line was malformed.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; index < lines.Length; index++)
        {
            var separator = lines[index].IndexOf(':');
            if (separator <= 0)
            {
                continue;
            }

            headers[lines[index][..separator].Trim()] = lines[index][(separator + 1)..].Trim();
        }

        var contentLength = headers.TryGetValue("Content-Length", out var contentLengthText)
            && int.TryParse(contentLengthText, out var parsedContentLength)
            ? parsedContentLength
            : 0;
        if (contentLength < 0 || contentLength > MaximumBodyBytes)
        {
            throw new InvalidDataException("The loopback request body exceeded the test bound.");
        }

        var bodyBytes = new byte[contentLength];
        var offset = 0;
        while (offset < bodyBytes.Length)
        {
            var read = await stream.ReadAsync(bodyBytes.AsMemory(offset), cancellationToken);
            if (read == 0)
            {
                throw new EndOfStreamException("The loopback client closed the request before sending its body.");
            }

            offset += read;
        }

        return new LoopbackHttpRequest(requestLine[0], requestLine[1], headers, Encoding.UTF8.GetString(bodyBytes));
    }

    private static async Task WriteResponseAsync(
        Stream stream,
        LoopbackHttpResponse response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        var bodyBytes = Encoding.UTF8.GetBytes(response.Body);
        var header = $"HTTP/1.1 {response.StatusCode} {StatusDescription(response.StatusCode)}\r\n"
            + $"Content-Type: {response.ContentType}\r\n"
            + $"Content-Length: {bodyBytes.Length}\r\n"
            + "Connection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(header), cancellationToken);
        await stream.WriteAsync(bodyBytes, cancellationToken);
    }

    private static bool EndsWithHeaderTerminator(byte[] buffer, int length) =>
        buffer[length - 4] == '\r'
        && buffer[length - 3] == '\n'
        && buffer[length - 2] == '\r'
        && buffer[length - 1] == '\n';

    private static string StatusDescription(int statusCode) =>
        Enum.IsDefined(typeof(HttpStatusCode), statusCode)
            ? ((HttpStatusCode)statusCode).ToString()
            : "Response";
}
