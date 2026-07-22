using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using Xunit;

namespace ChangeLens.Core.UnitTests.Git.Parsers;

/// <summary>
///     Verifies strict parsing of output from approved Git inspection commands.
/// </summary>
public sealed class GitOutputParserTests
{
    private const string Sha1Revision = "0123456789abcdef0123456789abcdef01234567";

    /// <summary>
    ///     Verifies that supported terminal line endings are removed from Git version output.
    /// </summary>
    /// <param name="lineEnding">The optional terminal line ending.</param>
    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ParseVersionAcceptsOneVersionLine(string lineEnding)
    {
        var output = new GitCommandOutput(0, "git version 2.51.0" + lineEnding, string.Empty);

        var result = GitOutputParser.ParseVersion(output);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    ///     Verifies that a nonzero version result reports that Git is unavailable.
    /// </summary>
    [Fact]
    public void ParseVersionReturnsUnavailableForNonzeroExit()
    {
        var output = new GitCommandOutput(127, string.Empty, "git: command not found");

        var result = GitOutputParser.ParseVersion(output);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal(GitErrorCode.Unavailable, error.Code);
        Assert.DoesNotContain("command not found", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Verifies that a nonzero version exit takes precedence over malformed output text.
    /// </summary>
    [Fact]
    public void ParseVersionReturnsUnavailableBeforeValidatingOutputShape()
    {
        var output = new GitCommandOutput(127, "unexpected\nextra\n", "git failed");

        var result = GitOutputParser.ParseVersion(output);

        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal(GitErrorCode.Unavailable, error.Code);
        Assert.DoesNotContain("unexpected", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Verifies that malformed version output is rejected.
    /// </summary>
    /// <param name="standardOutput">The malformed standard output.</param>
    [Theory]
    [InlineData("")]
    [InlineData("git version")]
    [InlineData("git version ")]
    [InlineData("Git version 2.51.0")]
    [InlineData("git version 2.51.0\r")]
    [InlineData("git version 2.51.0\nextra")]
    [InlineData("git version 2.51.0\n\n")]
    public void ParseVersionRejectsMalformedOutput(string standardOutput)
    {
        var result = GitOutputParser.ParseVersion(
            new GitCommandOutput(0, standardOutput, string.Empty));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that standard error invalidates an otherwise successful version result.
    /// </summary>
    [Fact]
    public void ParseVersionRejectsStandardErrorOnSuccess()
    {
        var result = GitOutputParser.ParseVersion(
            new GitCommandOutput(0, "git version 2.51.0\n", "unexpected warning"));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that exact lowercase Boolean values are accepted.
    /// </summary>
    /// <param name="text">The Boolean text emitted by Git.</param>
    /// <param name="expected">The expected Boolean value.</param>
    /// <param name="lineEnding">The optional terminal line ending.</param>
    [Theory]
    [InlineData("true", true, "")]
    [InlineData("true", true, "\n")]
    [InlineData("true", true, "\r\n")]
    [InlineData("false", false, "")]
    [InlineData("false", false, "\n")]
    [InlineData("false", false, "\r\n")]
    public void ParseBooleanAcceptsExactLowercaseValues(
        string text,
        bool expected,
        string lineEnding)
    {
        var output = new GitCommandOutput(0, text + lineEnding, string.Empty);

        var result = GitOutputParser.ParseBoolean(output);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Data);
    }

    /// <summary>
    ///     Verifies that values outside the exact Git Boolean contract are rejected.
    /// </summary>
    /// <param name="standardOutput">The invalid Boolean output.</param>
    [Theory]
    [InlineData("")]
    [InlineData("True")]
    [InlineData("FALSE")]
    [InlineData(" true")]
    [InlineData("true ")]
    [InlineData("1")]
    [InlineData("true\nfalse\n")]
    public void ParseBooleanRejectsUnexpectedValues(string standardOutput)
    {
        var result = GitOutputParser.ParseBoolean(
            new GitCommandOutput(0, standardOutput, string.Empty));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that Boolean parsing requires a successful, quiet Git command.
    /// </summary>
    /// <param name="exitCode">The Git process exit code.</param>
    /// <param name="standardError">The Git standard error text.</param>
    [Theory]
    [InlineData(1, "")]
    [InlineData(0, "warning")]
    public void ParseBooleanRejectsFailedOrNoisyOutput(int exitCode, string standardError)
    {
        var result = GitOutputParser.ParseBoolean(
            new GitCommandOutput(exitCode, "true\n", standardError));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that a fully qualified path is returned without arbitrary trimming.
    /// </summary>
    /// <param name="lineEnding">The optional terminal line ending.</param>
    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ParsePathAcceptsFullyQualifiedPath(string lineEnding)
    {
        var expected = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "change-lens-repository"));
        var output = new GitCommandOutput(0, expected + lineEnding, string.Empty);

        var result = GitOutputParser.ParsePath(output);

        Assert.True(result.IsSuccess);
        Assert.Equal(expected, result.Data);
    }

    /// <summary>
    ///     Verifies that a relative path is rejected.
    /// </summary>
    [Fact]
    public void ParsePathRejectsRelativePath()
    {
        var result = GitOutputParser.ParsePath(
            new GitCommandOutput(0, Path.Combine("relative", "repository") + "\n", string.Empty));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that path parsing rejects failed, noisy, and multi-line output.
    /// </summary>
    /// <param name="exitCode">The Git process exit code.</param>
    /// <param name="standardOutput">The Git standard output text.</param>
    /// <param name="standardError">The Git standard error text.</param>
    [Theory]
    [InlineData(1, "/repository\n", "")]
    [InlineData(0, "/repository\n", "warning")]
    [InlineData(0, "/repository\n/other\n", "")]
    public void ParsePathRejectsFailedNoisyOrMultiLineOutput(
        int exitCode,
        string standardOutput,
        string standardError)
    {
        var result = GitOutputParser.ParsePath(
            new GitCommandOutput(exitCode, standardOutput, standardError));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that full SHA-1 and SHA-256 revisions are accepted.
    /// </summary>
    /// <param name="revision">The full lowercase object identifier.</param>
    /// <param name="lineEnding">The optional terminal line ending.</param>
    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef01234567", "")]
    [InlineData("0123456789abcdef0123456789abcdef01234567", "\n")]
    [InlineData("0123456789abcdef0123456789abcdef01234567", "\r\n")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "\n")]
    public void ParseRevisionAcceptsSupportedObjectIds(string revision, string lineEnding)
    {
        var output = new GitCommandOutput(0, revision + lineEnding, string.Empty);

        var result = GitOutputParser.ParseRevision(output);

        Assert.True(result.IsSuccess);
        Assert.Equal(revision, result.Data);
    }

    /// <summary>
    ///     Verifies that unsupported object identifiers are rejected.
    /// </summary>
    /// <param name="revision">The unsupported object identifier.</param>
    [Theory]
    [InlineData("")]
    [InlineData("0123456789abcdef0123456789abcdef0123456")]
    [InlineData("0123456789abcdef0123456789abcdef012345678")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567")]
    [InlineData("g123456789abcdef0123456789abcdef01234567")]
    [InlineData("0123456789abcdef0123456789abcdef01234567 ")]
    public void ParseRevisionRejectsUnsupportedObjectIds(string revision)
    {
        var result = GitOutputParser.ParseRevision(
            new GitCommandOutput(0, revision + "\n", string.Empty));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that revision parsing requires a successful, quiet Git command.
    /// </summary>
    /// <param name="exitCode">The Git process exit code.</param>
    /// <param name="standardError">The Git standard error text.</param>
    [Theory]
    [InlineData(128, "")]
    [InlineData(0, "warning")]
    public void ParseRevisionRejectsFailedOrNoisyOutput(int exitCode, string standardError)
    {
        var result = GitOutputParser.ParseRevision(
            new GitCommandOutput(exitCode, Sha1Revision + "\n", standardError));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that a symbolic branch is represented with its full revision.
    /// </summary>
    /// <param name="lineEnding">The optional terminal line ending.</param>
    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void ParseHeadReturnsBranchForSuccessfulSymbolicRef(string lineEnding)
    {
        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(0, "feature/open-safely" + lineEnding, string.Empty),
            Sha1Revision);

        var head = Assert.IsType<BranchRepositoryHead>(result.Data);
        Assert.Equal("feature/open-safely", head.Name);
        Assert.Equal(Sha1Revision, head.Revision);
    }

    /// <summary>
    ///     Verifies that a quiet symbolic-ref miss is classified as detached HEAD.
    /// </summary>
    [Fact]
    public void ParseHeadReturnsDetachedForQuietExitOne()
    {
        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(1, string.Empty, string.Empty),
            Sha1Revision);

        var head = Assert.IsType<DetachedRepositoryHead>(result.Data);
        Assert.Equal(Sha1Revision, head.Revision);
    }

    /// <summary>
    ///     Verifies that empty or whitespace-only branch names are rejected.
    /// </summary>
    /// <param name="branchName">The invalid branch name.</param>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    [InlineData(" \t ")]
    public void ParseHeadRejectsWhitespaceBranch(string branchName)
    {
        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(0, branchName + "\n", string.Empty),
            Sha1Revision);

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that branch output must contain exactly one line.
    /// </summary>
    [Fact]
    public void ParseHeadRejectsExtraBranchLines()
    {
        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(0, "main\nother\n", string.Empty),
            Sha1Revision);

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that detached HEAD requires exactly empty output streams.
    /// </summary>
    /// <param name="standardOutput">The unexpected standard output.</param>
    /// <param name="standardError">The unexpected standard error.</param>
    [Theory]
    [InlineData("\n", "")]
    [InlineData("main\n", "")]
    [InlineData("", "warning")]
    public void ParseHeadRejectsNoisyDetachedResult(string standardOutput, string standardError)
    {
        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(1, standardOutput, standardError),
            Sha1Revision);

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that symbolic-ref exit codes other than zero and one are rejected.
    /// </summary>
    [Fact]
    public void ParseHeadRejectsUnexpectedExitCode()
    {
        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(128, string.Empty, string.Empty),
            Sha1Revision);

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that HEAD parsing rejects an unsupported supplied revision.
    /// </summary>
    [Fact]
    public void ParseHeadRejectsUnsupportedRevision()
    {
        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(0, "main\n", string.Empty),
            "not-a-revision");

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that invalid UTF-16 text from a fake boundary is rejected before parsing.
    /// </summary>
    /// <param name="invalidStandardOutput">
    ///     <see langword="true" /> to invalidate standard output; otherwise, standard error is invalid.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseHeadRejectsTextThatCannotBeEncodedAsUtf8(bool invalidStandardOutput)
    {
        var invalidText = new string('\ud800', 1);
        var standardOutput = invalidStandardOutput ? invalidText : "main\n";
        var standardError = invalidStandardOutput ? string.Empty : invalidText;

        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(0, standardOutput, standardError),
            Sha1Revision);

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that either output stream is rejected when its UTF-8 size exceeds 64 KiB.
    /// </summary>
    /// <param name="oversizeStandardOutput">
    ///     <see langword="true" /> to oversize standard output; otherwise, standard error is oversized.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ParseHeadRejectsOversizedStream(bool oversizeStandardOutput)
    {
        var oversized = new string('a', GitInspectionConstants.MaximumStreamBytes + 1);
        var standardOutput = oversizeStandardOutput ? oversized : "main\n";
        var standardError = oversizeStandardOutput ? string.Empty : oversized;

        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(0, standardOutput, standardError),
            Sha1Revision);

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that a stream exactly at the 64 KiB bound is not rejected as oversized.
    /// </summary>
    [Fact]
    public void ParseHeadAcceptsStreamAtMaximumSize()
    {
        var branchName = new string('a', GitInspectionConstants.MaximumStreamBytes);

        var result = GitOutputParser.ParseHead(
            new GitCommandOutput(0, branchName, string.Empty),
            Sha1Revision);

        var head = Assert.IsType<BranchRepositoryHead>(result.Data);
        Assert.Equal(branchName, head.Name);
    }

    private static void AssertInspectionFailure(Result result)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal(RepositoryErrorCode.InspectionFailed, error.Code);
    }
}
