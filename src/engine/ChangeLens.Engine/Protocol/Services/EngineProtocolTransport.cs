using System.Text;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Provides bounded newline-delimited JSON transport over the engine text streams.
/// </summary>
/// <remarks>
///     The host registers this service as a singleton and reads requests sequentially. It retains unread buffered
///     characters between requests and does not need to be thread-safe.
/// </remarks>
/// <param name="input">The text stream that supplies requests. Cannot be <see langword="null" />.</param>
/// <param name="output">The text stream that receives responses. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The protocol serializer. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for safe transport metadata. Cannot be <see langword="null" />.</param>
internal sealed class EngineProtocolTransport(
    TextReader input,
    TextWriter output,
    EngineProtocolSerializer protocolSerializer,
    ILogger<EngineProtocolTransport> logger) : IEngineProtocolTransport
{
    /// <summary>
    ///     Stores the fixed-size input chunk so oversized lines do not require oversized allocations.
    /// </summary>
    private readonly char[] _readBuffer = new char[EngineProtocolConstants.ReadBufferCharacterCount];

    /// <summary>
    ///     Tracks the next unread character in <see cref="_readBuffer" />.
    /// </summary>
    private int _readOffset;

    /// <summary>
    ///     Tracks the number of populated characters in <see cref="_readBuffer" />.
    /// </summary>
    private int _readCount;

    /// <summary>
    ///     Defers optional line-feed consumption after a carriage-return line ending.
    /// </summary>
    private bool _skipLineFeed;

    /// <inheritdoc />
    public async Task<Result<EngineProtocolRequest?>> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var lineResult = await ReadBoundedLineAsync(cancellationToken);
            if (lineResult.IsFailure)
            {
                logger.LogInformation(
                    "Rejected protocol input with errors {ErrorCodes}.",
                    lineResult.Errors.Select(error => error.Code).ToArray());
                return Result.ErrorFromResult<EngineProtocolRequest?>(lineResult);
            }

            if (lineResult.Data is null)
            {
                return Result.Success<EngineProtocolRequest?>(null);
            }

            var requestResult = protocolSerializer.DeserializeRequest(lineResult.Data);
            if (requestResult.IsFailure)
            {
                logger.LogInformation(
                    "Rejected protocol input with errors {ErrorCodes}.",
                    requestResult.Errors.Select(error => error.Code).ToArray());
                return Result.ErrorFromResult<EngineProtocolRequest?>(requestResult);
            }

            var request = requestResult.Data!;
            logger.LogDebug(
                "Decoded protocol request {RequestId} for {Action}.",
                request.RequestId,
                request.Action);
            return Result.Success<EngineProtocolRequest?>(request);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            logger.LogError(
                exception,
                "Failed to read protocol input with error {ErrorCode}.",
                EngineProtocolConstants.ReadFailedErrorCode);
            return Result.Fail<EngineProtocolRequest?>(
                OperationError.ExternalDependencyFailure(
                    "The engine could not read protocol input.",
                    EngineProtocolConstants.ReadFailedErrorCode));
        }
    }

    /// <inheritdoc />
    public async Task<Result> WriteAsync(
        ProtocolResponse response,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);

        var serializationResult = protocolSerializer.SerializeResponse(response);
        if (serializationResult.IsFailure)
        {
            return Result.ErrorFromResult(serializationResult);
        }

        try
        {
            await output.WriteLineAsync(serializationResult.Data.AsMemory(), cancellationToken);
            await output.FlushAsync(cancellationToken);
            logger.LogDebug(
                "Wrote protocol response {RequestId} of type {ResponseType}.",
                response.RequestId,
                response.Type);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            logger.LogError(
                exception,
                "Failed to write protocol output with error {ErrorCode}.",
                EngineProtocolConstants.WriteFailedErrorCode);
            return Result.Fail(
                OperationError.ExternalDependencyFailure(
                    "The engine could not write protocol output.",
                    EngineProtocolConstants.WriteFailedErrorCode));
        }
    }

    /// <summary>
    ///     Asynchronously reads one line while bounding retained request content.
    /// </summary>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for input.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the line, successful
    ///     <see langword="null" /> at end of input, or a request-too-large failure.
    /// </returns>
    private async Task<Result<string?>> ReadBoundedLineAsync(CancellationToken cancellationToken)
    {
        var builder = new StringBuilder(EngineProtocolConstants.ReadBufferCharacterCount);
        var tooLarge = false;

        while (true)
        {
            if (_readOffset == _readCount)
            {
                _readCount = await input.ReadAsync(_readBuffer.AsMemory(), cancellationToken);
                _readOffset = 0;

                if (_readCount == 0)
                {
                    if (builder.Length == 0 && !tooLarge)
                    {
                        return Result.Success<string?>(null);
                    }

                    return CompleteLine(builder, tooLarge);
                }
            }

            var character = _readBuffer[_readOffset++];

            if (_skipLineFeed)
            {
                _skipLineFeed = false;
                if (character == '\n')
                {
                    continue;
                }
            }

            if (character == '\r')
            {
                _skipLineFeed = true;
                return CompleteLine(builder, tooLarge);
            }

            if (character == '\n')
            {
                return CompleteLine(builder, tooLarge);
            }

            if (tooLarge)
            {
                continue;
            }

            if (builder.Length == EngineProtocolConstants.MaximumRequestCharacterCount)
            {
                tooLarge = true;
                continue;
            }

            builder.Append(character);
        }
    }

    /// <summary>
    ///     Creates the decoded line or the stable failure for discarded oversized content.
    /// </summary>
    /// <param name="builder">The bounded line content.</param>
    /// <param name="tooLarge">
    ///     <see langword="true" /> when additional content was discarded; otherwise, <see langword="false" />.
    /// </param>
    /// <returns>The completed line or the request-too-large failure.</returns>
    private static Result<string?> CompleteLine(StringBuilder builder, bool tooLarge) =>
        tooLarge
            ? Result.Fail<string?>(
                OperationError.MalformedInput(
                    "The protocol request exceeds the allowed size.",
                    EngineProtocolConstants.RequestTooLargeErrorCode))
            : Result.Success<string?>(builder.ToString());
}
