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
    ///     Parses the configured remote names from <c>git remote</c> output.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the configured remote names in Git output order.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<IReadOnlyList<string>> ParseRemoteNames(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.ExitCode != 0 || output.StandardError.Length != 0)
        {
            return InspectionFailure<IReadOnlyList<string>>();
        }

        if (output.StandardOutput.Length == 0)
        {
            return Result.Success<IReadOnlyList<string>>(Array.Empty<string>());
        }

        if (!output.StandardOutput.EndsWith('\n') || output.StandardOutput.Contains('\r'))
        {
            return InspectionFailure<IReadOnlyList<string>>();
        }

        var lines = output.StandardOutput.Split('\n');
        var names = new List<string>(lines.Length - 1);
        for (var index = 0; index < lines.Length - 1; index++)
        {
            if (lines[index].Length == 0)
            {
                return InspectionFailure<IReadOnlyList<string>>();
            }

            names.Add(lines[index]);
        }

        return lines[^1].Length == 0
            ? Result.Success<IReadOnlyList<string>>(names)
            : InspectionFailure<IReadOnlyList<string>>();
    }

    /// <summary>
    ///     Parses the single advertised revision for one branch from <c>git ls-remote --heads</c> output.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <param name="expectedRef">The full <c>refs/heads/</c> reference requested. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the advertised full object identifier on success.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> or <paramref name="expectedRef" /> is <see langword="null" />.
    /// </exception>
    internal static Result<string> ParseLsRemoteRevision(
        GitCommandOutput output,
        string expectedRef)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(expectedRef);

        if (output.ExitCode != 0 ||
            output.StandardError.Length != 0 ||
            !output.StandardOutput.EndsWith('\n') ||
            output.StandardOutput.Contains('\r'))
        {
            return InspectionFailure<string>();
        }

        var lines = output.StandardOutput.Split('\n');
        if (lines.Length != 2 || lines[1].Length != 0)
        {
            return InspectionFailure<string>();
        }

        var fields = lines[0].Split('\t');
        return fields.Length == 2 &&
               IsSupportedObjectId(fields[0]) &&
               string.Equals(fields[1], expectedRef, StringComparison.Ordinal)
            ? Result.Success<string>(fields[0])
            : InspectionFailure<string>();
    }

    /// <summary>
    ///     Resolves the configured remote and branch encoded in a cached remote-tracking reference.
    /// </summary>
    /// <remarks>
    ///     Splitting at the first separator after the <c>refs/remotes/</c> prefix is incorrect because branch names
    ///     routinely contain slashes. This selects the longest configured remote name that prefixes the reference
    ///     suffix, which is the only unambiguous resolution without querying Git for the split directly.
    /// </remarks>
    /// <param name="target">The full remote-tracking reference. Cannot be <see langword="null" />.</param>
    /// <param name="remoteNames">The configured remote names. Cannot be <see langword="null" />.</param>
    /// <returns>The resolved remote name and branch name, or <see langword="null" /> when none matches.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="target" /> or <paramref name="remoteNames" /> is <see langword="null" />.
    /// </exception>
    internal static (string Remote, string Branch)? ResolveRemoteBranch(
        string target,
        IReadOnlyList<string> remoteNames)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(remoteNames);

        const string prefix = "refs/remotes/";
        if (!target.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var suffix = target[prefix.Length..];
        string? bestRemote = null;

        foreach (var remoteName in remoteNames)
        {
            if (remoteName.Length == 0 ||
                suffix.Length <= remoteName.Length ||
                suffix[remoteName.Length] != '/' ||
                !suffix.StartsWith(remoteName, StringComparison.Ordinal))
            {
                continue;
            }

            if (bestRemote is null || remoteName.Length > bestRemote.Length)
            {
                bestRemote = remoteName;
            }
        }

        if (bestRemote is null)
        {
            return null;
        }

        var branch = suffix[(bestRemote.Length + 1)..];
        return branch.Length == 0 ? null : (bestRemote, branch);
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
