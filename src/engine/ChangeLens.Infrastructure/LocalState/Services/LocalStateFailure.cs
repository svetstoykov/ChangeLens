using ChangeLens.Core.LocalState.Constants;
using ChangeLens.Core.Results.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Provides stable local-state failures and expected exception classification.
/// </summary>
internal static class LocalStateFailure
{
    private static readonly OperationError UnavailableError = OperationError.ExternalDependencyFailure(
        "ChangeLens local state is unavailable. Review the Engine logs and retry.",
        LocalStateErrorCode.Unavailable);

    private static readonly OperationError InvalidError = OperationError.UnprocessableInput(
        "The existing local-state database contains invalid metadata.",
        LocalStateErrorCode.Invalid);

    /// <summary>
    ///     Maps an expected SQLite or filesystem access failure to the stable local-state result.
    /// </summary>
    /// <param name="exception">The expected access exception, or <see langword="null" />.</param>
    /// <returns>The stable unavailable result.</returns>
    internal static Result Unavailable(Exception? exception = null) => Result.Fail(UnavailableError);

    /// <summary>
    ///     Maps an expected SQLite or filesystem access failure to a typed local-state result.
    /// </summary>
    /// <typeparam name="T">The discarded success payload type.</typeparam>
    /// <param name="exception">The expected access exception, or <see langword="null" />.</param>
    /// <returns>The stable unavailable result.</returns>
    internal static Result<T> Unavailable<T>(Exception? exception = null) => UnavailableError;

    /// <summary>
    ///     Maps malformed stored metadata to the stable invalid local-state result.
    /// </summary>
    /// <typeparam name="T">The discarded success payload type.</typeparam>
    /// <returns>The stable invalid local-state result.</returns>
    internal static Result<T> Invalid<T>() => InvalidError;

    /// <summary>
    ///     Determines whether an exception is an expected local database access failure.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true" /> when the exception is an expected local-state failure.</returns>
    internal static bool IsExpectedAccessFailure(Exception exception) =>
        exception is SqliteException or IOException or UnauthorizedAccessException or DbUpdateException;

    /// <summary>
    ///     Determines whether an exception represents malformed typed metadata read from SQLite.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <returns><see langword="true" /> when stored metadata cannot satisfy its owned model.</returns>
    internal static bool IsMalformedDataFailure(Exception exception) =>
        exception is FormatException or InvalidCastException or OverflowException;
}
