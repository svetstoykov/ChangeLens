using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Provides controlled protocol read and write outcomes for host lifecycle tests.
/// </summary>
internal sealed class StubEngineProtocolTransport(
    Func<CancellationToken, Task<Result<EngineProtocolRequest?>>> readAsync,
    Func<ProtocolResponse, CancellationToken, Task<Result>> writeAsync) : IEngineProtocolTransport
{
    /// <summary>
    ///     Gets the number of protocol read attempts.
    /// </summary>
    internal int ReadCount { get; private set; }

    /// <summary>
    ///     Gets the number of protocol write attempts.
    /// </summary>
    internal int WriteCount { get; private set; }

    /// <inheritdoc />
    public Task<Result<EngineProtocolRequest?>> ReadAsync(CancellationToken cancellationToken)
    {
        ReadCount++;
        return readAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<Result> WriteAsync(
        ProtocolResponse response,
        CancellationToken cancellationToken)
    {
        WriteCount++;
        return writeAsync(response, cancellationToken);
    }
}
