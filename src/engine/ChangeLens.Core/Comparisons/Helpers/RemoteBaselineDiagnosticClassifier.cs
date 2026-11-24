using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Comparisons.Helpers;

/// <summary>
///     Provides safe classification of reviewed C-locale remote Git diagnostics.
/// </summary>
internal static class RemoteBaselineDiagnosticClassifier
{
    /// <summary>
    ///     Classifies a failed remote Git command (<c>ls-remote</c> or <c>fetch</c>) without exposing Git output.
    /// </summary>
    /// <param name="output">The captured failed Git output. Cannot be <see langword="null" />.</param>
    /// <returns>The stable safe operation error for the recognized failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static OperationError ClassifyRemoteFailure(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (output.ExitCode == 0 ||
            !GitDiagnosticClassifier.IsReviewable(output.StandardOutput) ||
            !GitDiagnosticClassifier.IsReviewable(output.StandardError))
        {
            return RemoteUnreachableFailure();
        }

        var diagnostic = output.StandardError;
        if (diagnostic.Contains("could not read username", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("could not read password", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("authentication failed", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("invalid username or password", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("permission denied (publickey", StringComparison.OrdinalIgnoreCase) ||
            diagnostic.Contains("terminal prompts disabled", StringComparison.OrdinalIgnoreCase))
        {
            return OperationError.Unauthorized(
                "The remote requires authentication. Run `git fetch <remote> <branch>` in a terminal, then try again.",
                ComparisonErrorCode.RemoteAuthenticationRequired);
        }

        return RemoteUnreachableFailure();
    }

    private static OperationError RemoteUnreachableFailure() =>
        OperationError.ExternalDependencyFailure("The remote could not be reached.", ComparisonErrorCode.RemoteUnreachable);
}
