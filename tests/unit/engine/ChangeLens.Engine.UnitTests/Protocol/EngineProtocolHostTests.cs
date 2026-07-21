using ChangeLens.Core.Results.Models;
using System.Text.Json;
using ChangeLens.Engine.EngineInformation.Services;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.UnitTests.Support;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Protocol;

/// <summary>
///     Verifies isolated request-boundary behavior of the hosted protocol boundary.
/// </summary>
public sealed class EngineProtocolHostTests
{
    /// <summary>
    ///     Verifies that a common request with an unknown method is rejected.
    /// </summary>
    [Fact]
    public void DispatchRequestRejectsUnknownMethod()
    {
        var service = CreateService();
        var request = new EngineProtocolRequest
        {
            ProtocolVersion = EngineProtocolConstants.CurrentVersion,
            RequestId = "request-method",
            Method = "analysis.run",
            Params = JsonDocument.Parse("{}").RootElement.Clone(),
        };

        var result = service.DispatchRequest(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal(EngineProtocolConstants.UnknownMethodErrorCode, error.Code);
    }

    /// <summary>
    ///     Verifies that an unexpected action exception is logged once and sanitized.
    /// </summary>
    [Fact]
    public void DispatchSafelySanitizesUnexpectedActionException()
    {
        var logger = new TestLogger<EngineProtocolHost>();
        var service = CreateService(logger: logger);
        var request = new ThrowingProtocolRequest();

        var result = service.DispatchSafely(request);

        var error = Assert.Single(result.Errors);
        Assert.Equal(EngineProtocolConstants.UnexpectedFailureErrorCode, error.Code);
        Assert.DoesNotContain("sensitive", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, logger.ErrorCount);
        Assert.IsType<InvalidOperationException>(logger.LastException);
    }

    /// <summary>
    ///     Verifies that host shutdown cancellation stops the protocol boundary normally.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task StopAsyncCancelsProtocolInputWithoutAnErrorLog()
    {
        var logger = new TestLogger<EngineProtocolHost>();
        var lifetime = new TestHostApplicationLifetime();
        var service = new EngineProtocolHost(
            new BlockingTextReader(),
            TextWriter.Null,
            new EngineProtocolRequestSerializer(),
            new EngineInformationProvider(),
            logger,
            lifetime);

        await service.StartAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await service.StopAsync(timeout.Token);

        Assert.True(lifetime.StopRequested);
        Assert.Equal(0, logger.ErrorCount);
    }

    /// <summary>
    ///     Creates an isolated host with the real engine-information capability.
    /// </summary>
    /// <param name="logger">The optional test logger.</param>
    /// <returns>The configured protocol host.</returns>
    private static EngineProtocolHost CreateService(TestLogger<EngineProtocolHost>? logger = null) =>
        new(
            TextReader.Null,
            TextWriter.Null,
            new EngineProtocolRequestSerializer(),
            new EngineInformationProvider(),
            logger ?? new TestLogger<EngineProtocolHost>(),
            new TestHostApplicationLifetime());
}
