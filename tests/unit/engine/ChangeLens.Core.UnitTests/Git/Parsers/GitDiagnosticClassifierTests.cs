using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;
using Xunit;

namespace ChangeLens.Core.UnitTests.Git.Parsers;

/// <summary>
///     Verifies safe classification of reviewed Git diagnostics.
/// </summary>
public sealed class GitDiagnosticClassifierTests
{
    /// <summary>
    ///     Verifies that a non-repository diagnostic is classified as unprocessable input.
    /// </summary>
    [Fact]
    public void ClassifyWorkTreeFailureReturnsNotGitRepository()
    {
        var output = new GitCommandOutput(
            128,
            string.Empty,
            "fatal: not a git repository (or any of the parent directories): .git");

        var error = GitDiagnosticClassifier.ClassifyWorkTreeFailure(output);

        Assert.Equal(ErrorType.UnprocessableInput, error.Type);
        Assert.Equal(RepositoryErrorCode.NotGitRepository, error.Code);
        Assert.DoesNotContain(".git", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Verifies that reviewed ownership and permission diagnostics are classified as access failures.
    /// </summary>
    /// <param name="diagnostic">The reviewed C-locale diagnostic.</param>
    [Theory]
    [InlineData("fatal: detected dubious ownership in repository at '/secret/repository'")]
    [InlineData("fatal: unsafe repository ('/secret/repository' is owned by someone else)")]
    [InlineData("fatal: could not open '/secret/repository': Permission Denied")]
    public void ClassifyInspectionFailureReturnsAccessDenied(string diagnostic)
    {
        var error = GitDiagnosticClassifier.ClassifyInspectionFailure(
            new GitCommandOutput(128, string.Empty, diagnostic));

        Assert.Equal(ErrorType.Unauthorized, error.Type);
        Assert.Equal(RepositoryErrorCode.AccessDenied, error.Code);
        Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Verifies that reviewed unborn and invalid HEAD diagnostics report an unavailable HEAD.
    /// </summary>
    /// <param name="diagnostic">The reviewed C-locale diagnostic.</param>
    [Theory]
    [InlineData("fatal: your current branch 'main' does not have any commits yet")]
    [InlineData("fatal: needed a single revision")]
    [InlineData("fatal: unknown revision or path not in the working tree")]
    [InlineData("fatal: bad revision 'HEAD^{commit}'")]
    [InlineData("fatal: ambiguous argument 'HEAD^{commit}': unknown revision")]
    public void ClassifyInspectionFailureReturnsHeadUnavailable(string diagnostic)
    {
        var error = GitDiagnosticClassifier.ClassifyInspectionFailure(
            new GitCommandOutput(128, string.Empty, diagnostic));

        Assert.Equal(ErrorType.UnprocessableInput, error.Type);
        Assert.Equal(RepositoryErrorCode.HeadUnavailable, error.Code);
        Assert.DoesNotContain("main", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Verifies that matching of reviewed diagnostics ignores character casing.
    /// </summary>
    [Fact]
    public void ClassifyInspectionFailureMatchesReviewedDiagnosticsIgnoringCase()
    {
        var error = GitDiagnosticClassifier.ClassifyInspectionFailure(
            new GitCommandOutput(128, string.Empty, "FATAL: NOT A GIT REPOSITORY"));

        Assert.Equal(ErrorType.UnprocessableInput, error.Type);
        Assert.Equal(RepositoryErrorCode.NotGitRepository, error.Code);
    }

    /// <summary>
    ///     Verifies that unknown diagnostics are never returned as user-facing text.
    /// </summary>
    [Fact]
    public void ClassifyInspectionFailureSanitizesUnknownDiagnostic()
    {
        var error = GitDiagnosticClassifier.ClassifyInspectionFailure(
            new GitCommandOutput(128, string.Empty, "secret repository detail"));

        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal(RepositoryErrorCode.InspectionFailed, error.Code);
        Assert.DoesNotContain("secret", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Verifies that a reviewed fragment on standard output cannot control classification.
    /// </summary>
    [Fact]
    public void ClassifyInspectionFailureDoesNotTrustStandardOutputAsDiagnostic()
    {
        var error = GitDiagnosticClassifier.ClassifyInspectionFailure(
            new GitCommandOutput(128, "not a git repository", "unreviewed failure"));

        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal(RepositoryErrorCode.InspectionFailed, error.Code);
    }

    /// <summary>
    ///     Verifies that contradictory successful output uses the generic safe failure.
    /// </summary>
    [Fact]
    public void ClassifyInspectionFailureReturnsFallbackForZeroExitCode()
    {
        var error = GitDiagnosticClassifier.ClassifyInspectionFailure(
            new GitCommandOutput(0, string.Empty, "not a git repository"));

        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal(RepositoryErrorCode.InspectionFailed, error.Code);
    }
}
