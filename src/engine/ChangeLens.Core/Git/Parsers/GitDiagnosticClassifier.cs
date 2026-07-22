using System.Text;
using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Parsers;

/// <summary>
///     Provides safe classification of reviewed C-locale Git diagnostics.
/// </summary>
internal static class GitDiagnosticClassifier
{
    /// <summary>
    ///     Rejects unpaired UTF-16 surrogates instead of replacing them while reviewing diagnostics.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    ///     Classifies a failed worktree detection command without exposing Git output.
    /// </summary>
    /// <param name="output">The captured failed Git output. Cannot be <see langword="null" />.</param>
    /// <returns>The stable safe operation error for the recognized failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static OperationError ClassifyWorkTreeFailure(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return Classify(output);
    }

    /// <summary>
    ///     Classifies a failed repository inspection command without exposing Git output.
    /// </summary>
    /// <param name="output">The captured failed Git output. Cannot be <see langword="null" />.</param>
    /// <returns>The stable safe operation error for the recognized failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static OperationError ClassifyInspectionFailure(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return Classify(output);
    }

    /// <summary>
    ///     Applies the reviewed diagnostic fragments to a failed Git command.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>The stable safe operation error for the recognized failure.</returns>
    private static OperationError Classify(GitCommandOutput output)
    {
        if (output.ExitCode == 0 ||
            !IsReviewable(output.StandardOutput) ||
            !IsReviewable(output.StandardError))
        {
            return InspectionFailure();
        }

        var diagnostic = output.StandardError;
        if (diagnostic.Contains("not a git repository", StringComparison.OrdinalIgnoreCase))
        {
            return OperationError.UnprocessableInput(
                "The selected folder is not inside a Git working tree.",
                RepositoryErrorCode.NotGitRepository);
        }

        if (diagnostic.Contains("detected dubious ownership in repository", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("unsafe repository", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("permission denied", StringComparison.OrdinalIgnoreCase))
        {
            return OperationError.Unauthorized(
                "Git could not access the selected repository.",
                RepositoryErrorCode.AccessDenied);
        }

        if (diagnostic.Contains("does not have any commits yet", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("needed a single revision", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("unknown revision or path not in the working tree", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("bad revision 'HEAD^{commit}'", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("ambiguous argument 'HEAD^{commit}'", StringComparison.OrdinalIgnoreCase))
        {
            return OperationError.UnprocessableInput(
                "The selected repository does not have a committed HEAD revision.",
                RepositoryErrorCode.HeadUnavailable);
        }

        return InspectionFailure();
    }

    /// <summary>
    ///     Determines whether a captured stream is valid UTF-8 text within the inspection byte bound.
    /// </summary>
    /// <param name="value">The decoded stream text. Cannot be <see langword="null" />.</param>
    /// <returns>
    ///     <see langword="true" /> when the text can be safely reviewed; otherwise, <see langword="false" />.
    /// </returns>
    private static bool IsReviewable(string value)
    {
        try
        {
            return StrictUtf8.GetByteCount(value) <= GitInspectionConstants.MaximumStreamBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Creates the safe fallback for an unrecognized or contradictory Git failure.
    /// </summary>
    /// <returns>The stable generic repository inspection error.</returns>
    private static OperationError InspectionFailure() =>
        OperationError.ExternalDependencyFailure(
            "Git repository inspection failed.",
            RepositoryErrorCode.InspectionFailed);
}
