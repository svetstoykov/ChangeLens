using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Provides transport mapping from capability outcomes to engine protocol responses.
/// </summary>
internal static class ProtocolResponseFactory
{
    /// <summary>
    ///     Creates a correlated result response for a direct value.
    /// </summary>
    /// <typeparam name="T">The result payload type.</typeparam>
    /// <param name="requestId">The request identifier. Cannot be <see langword="null" />.</param>
    /// <param name="value">The result payload.</param>
    /// <returns>A typed protocol result response.</returns>
    internal static ProtocolResultResponse<T> CreateValue<T>(string requestId, T value) =>
        new(
            EngineProtocolConstants.CurrentVersion,
            EngineProtocolConstants.ResultResponseType,
            requestId,
            value);

    /// <summary>
    ///     Maps a payload-free Result to a correlated result or error response.
    /// </summary>
    /// <param name="requestId">The request identifier. Cannot be <see langword="null" />.</param>
    /// <param name="result">The capability result. Cannot be <see langword="null" />.</param>
    /// <returns>A payload-free result response on success; otherwise, an ordered error response.</returns>
    internal static object FromResult(string requestId, Result result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsFailure
            ? CreateError(requestId, result.Errors)
            : CreateValue<object?>(requestId, null);
    }

    /// <summary>
    ///     Maps a typed Result to a correlated result or error response.
    /// </summary>
    /// <typeparam name="T">The result payload type.</typeparam>
    /// <param name="requestId">The request identifier. Cannot be <see langword="null" />.</param>
    /// <param name="result">The capability result. Cannot be <see langword="null" />.</param>
    /// <returns>A typed result response on success; otherwise, an ordered error response.</returns>
    internal static object FromResult<T>(string requestId, Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsFailure
            ? CreateError(requestId, result.Errors)
            : CreateValue(requestId, result.Data);
    }

    /// <summary>
    ///     Creates an error response while preserving every valid source error in order.
    /// </summary>
    /// <param name="requestId">The request identifier, or <see langword="null" /> when unavailable.</param>
    /// <param name="errors">The source errors. Cannot be <see langword="null" />.</param>
    /// <returns>The ordered error response, or a sanitized internal error for an invalid source contract.</returns>
    internal static ProtocolErrorResponse CreateError(
        string? requestId,
        IReadOnlyList<OperationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0 || errors.Any(
                error => string.IsNullOrWhiteSpace(error.Code) ||
                         string.IsNullOrWhiteSpace(error.Message)))
        {
            return CreateUnexpectedFailure(requestId);
        }

        return new ProtocolErrorResponse(
            EngineProtocolConstants.CurrentVersion,
            EngineProtocolConstants.ErrorResponseType,
            requestId,
            errors.Select(error => new ProtocolError(error.Type, error.Code!, error.Message)).ToArray());
    }

    /// <summary>
    ///     Creates the sanitized error returned for an unexpected or invalid internal condition.
    /// </summary>
    /// <param name="requestId">The request identifier, or <see langword="null" /> when unavailable.</param>
    /// <returns>A response containing one stable internal error.</returns>
    internal static ProtocolErrorResponse CreateUnexpectedFailure(string? requestId) =>
        new(
            EngineProtocolConstants.CurrentVersion,
            EngineProtocolConstants.ErrorResponseType,
            requestId,
            [
                new ProtocolError(
                    ErrorType.InternalError,
                    EngineProtocolConstants.UnexpectedFailureErrorCode,
                    EngineProtocolConstants.UnexpectedFailureMessage),
            ]);
}
