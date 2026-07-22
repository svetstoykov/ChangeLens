using System.Text;
using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Parsers;

/// <summary>
///     Provides strict parsing for output from approved Git inspection commands.
/// </summary>
internal static class GitOutputParser
{
    /// <summary>
    ///     Rejects unpaired UTF-16 surrogates instead of replacing them while measuring UTF-8 output.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    ///     Validates output from the Git availability command.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A successful result when the output contains one valid Git version line; otherwise, a failed result.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result ParseVersion(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.ExitCode != 0)
        {
            return Result.Fail(
                OperationError.ExternalDependencyFailure(
                    "Git is unavailable.",
                    GitErrorCode.Unavailable));
        }

        var lineResult = ParseSingleLine(output);
        if (lineResult.IsFailure)
        {
            return Result.ErrorFromResult(lineResult);
        }

        const string versionPrefix = "git version ";
        var value = lineResult.Data!;
        return output.StandardError.Length == 0 &&
               value.StartsWith(versionPrefix, StringComparison.Ordinal) &&
               !string.IsNullOrWhiteSpace(value[versionPrefix.Length..])
            ? Result.Success()
            : InspectionFailure();
    }

    /// <summary>
    ///     Parses an exact lowercase Boolean value from a successful Git command.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the parsed Boolean value on success.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<bool> ParseBoolean(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var lineResult = ParseSuccessfulSingleLine(output);
        if (lineResult.IsFailure)
        {
            return Result.ErrorFromResult<bool>(lineResult);
        }

        return lineResult.Data switch
        {
            "true" => Result.Success(true),
            "false" => Result.Success(false),
            _ => InspectionFailure<bool>(),
        };
    }

    /// <summary>
    ///     Parses a fully qualified path from a successful Git command.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the fully qualified path on success.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<string> ParsePath(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var lineResult = ParseSuccessfulSingleLine(output);
        if (lineResult.IsFailure)
        {
            return Result.ErrorFromResult<string>(lineResult);
        }

        return Path.IsPathFullyQualified(lineResult.Data!)
            ? lineResult
            : InspectionFailure<string>();
    }

    /// <summary>
    ///     Parses a full lowercase SHA-1 or SHA-256 object identifier from a successful Git command.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the full lowercase object identifier on success.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<string> ParseRevision(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var lineResult = ParseSuccessfulSingleLine(output);
        if (lineResult.IsFailure)
        {
            return Result.ErrorFromResult<string>(lineResult);
        }

        return IsSupportedObjectId(lineResult.Data!)
            ? lineResult
            : InspectionFailure<string>();
    }

    /// <summary>
    ///     Parses an attached branch or detached HEAD state from symbolic-ref output.
    /// </summary>
    /// <param name="output">The captured symbolic-ref output. Cannot be <see langword="null" />.</param>
    /// <param name="revision">
    ///     The previously parsed full object identifier. Cannot be <see langword="null" /> or empty.
    /// </param>
    /// <returns>A result containing the typed repository HEAD state on success.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<RepositoryHead> ParseHead(
        GitCommandOutput output,
        string revision)
    {
        ArgumentNullException.ThrowIfNull(output);

        var lineResult = ParseSingleLine(output);
        if (lineResult.IsFailure)
        {
            return Result.ErrorFromResult<RepositoryHead>(lineResult);
        }

        if (!IsSupportedObjectId(revision))
        {
            return InspectionFailure<RepositoryHead>();
        }

        if (output.ExitCode == 0 &&
            output.StandardError.Length == 0 &&
            !string.IsNullOrWhiteSpace(lineResult.Data))
        {
            return Result.Success<RepositoryHead>(
                new BranchRepositoryHead(lineResult.Data!, revision));
        }

        if (output.ExitCode == 1 &&
            output.StandardOutput.Length == 0 &&
            output.StandardError.Length == 0)
        {
            return Result.Success<RepositoryHead>(new DetachedRepositoryHead(revision));
        }

        return InspectionFailure<RepositoryHead>();
    }

    /// <summary>
    ///     Requires successful, quiet output before returning its single parsed line.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the single output line on success.</returns>
    private static Result<string> ParseSuccessfulSingleLine(GitCommandOutput output)
    {
        var lineResult = ParseSingleLine(output);
        if (lineResult.IsFailure)
        {
            return lineResult;
        }

        return output.ExitCode == 0 && output.StandardError.Length == 0
            ? lineResult
            : InspectionFailure<string>();
    }

    /// <summary>
    ///     Parses one output line while preserving all non-terminal whitespace.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the line after one optional terminal LF or CRLF is removed.</returns>
    private static Result<string> ParseSingleLine(GitCommandOutput output)
    {
        if (IsOversized(output.StandardOutput) || IsOversized(output.StandardError))
        {
            return InspectionFailure<string>();
        }

        var value = output.StandardOutput.EndsWith("\r\n", StringComparison.Ordinal)
            ? output.StandardOutput[..^2]
            : output.StandardOutput.EndsWith('\n')
                ? output.StandardOutput[..^1]
                : output.StandardOutput;

        return value.Contains('\r') || value.Contains('\n')
            ? InspectionFailure<string>()
            : Result.Success<string>(value);
    }

    /// <summary>
    ///     Determines whether text exceeds the byte bound or cannot be represented as valid UTF-8.
    /// </summary>
    /// <param name="value">The decoded output text. Cannot be <see langword="null" />.</param>
    /// <returns>
    ///     <see langword="true" /> when the text is invalid or oversized; otherwise, <see langword="false" />.
    /// </returns>
    private static bool IsOversized(string value)
    {
        try
        {
            return StrictUtf8.GetByteCount(value) > GitInspectionConstants.MaximumStreamBytes;
        }
        catch (EncoderFallbackException)
        {
            return true;
        }
    }

    /// <summary>
    ///     Determines whether a value is a supported full lowercase Git object identifier.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns>
    ///     <see langword="true" /> for a 40- or 64-character lowercase hexadecimal value; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    private static bool IsSupportedObjectId(string? value) =>
        value is { Length: 40 or 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary>
    ///     Creates a safe generic repository inspection failure.
    /// </summary>
    /// <returns>A failed result with the stable repository inspection error.</returns>
    private static Result InspectionFailure() =>
        Result.Fail(
            OperationError.ExternalDependencyFailure(
                "Git repository inspection failed.",
                RepositoryErrorCode.InspectionFailed));

    /// <summary>
    ///     Creates a typed safe generic repository inspection failure.
    /// </summary>
    /// <typeparam name="T">The success payload type.</typeparam>
    /// <returns>A failed result with the stable repository inspection error.</returns>
    private static Result<T> InspectionFailure<T>() =>
        Result.Fail<T>(
            OperationError.ExternalDependencyFailure(
                "Git repository inspection failed.",
                RepositoryErrorCode.InspectionFailed));
}
