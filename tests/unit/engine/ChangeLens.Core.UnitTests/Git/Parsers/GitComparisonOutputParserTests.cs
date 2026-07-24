using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Git.Parsers;
using ChangeLens.Core.Results.Models;
using System.Text;
using Xunit;

namespace ChangeLens.Core.UnitTests.Git.Parsers;

/// <summary>
///     Verifies strict parsing of reviewed Git comparison output.
/// </summary>
public sealed class GitComparisonOutputParserTests
{
    private const string Sha1Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string OtherSha1Revision = "89abcdef0123456789abcdef0123456789abcdef";
    private const string ZeroSha1Revision = "0000000000000000000000000000000000000000";
    private const string Sha256Revision =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OtherSha256Revision =
        "89abcdef0123456789abcdef0123456789abcdef0123456789abcdef01234567";

    /// <summary>
    ///     Verifies that local, cached remote, and symbolic target records preserve reviewed fields.
    /// </summary>
    [Fact]
    public void ParseTargetRecordsAcceptsReviewedRecords()
    {
        var standardOutput =
            $"refs/heads/main\0{Sha1Revision}\0commit\0\0origin\0\n" +
            $"refs/remotes/upstream/main\0{Sha256Revision}\0commit\0\0\0\n" +
            $"refs/remotes/origin/HEAD\0{OtherSha1Revision}\0commit\0refs/remotes/origin/main\0\0\n";

        var result = GitComparisonOutputParser.ParseTargetRecords(Success(standardOutput));

        Assert.True(result.IsSuccess);
        var records = Assert.IsAssignableFrom<IReadOnlyList<GitComparisonTargetRecord>>(result.Data);
        Assert.Collection(
            records,
            record =>
            {
                Assert.Equal("refs/heads/main", record.FullName);
                Assert.Equal(Sha1Revision, record.Revision);
                Assert.Equal("commit", record.ObjectType);
                Assert.Null(record.SymbolicTarget);
                Assert.Equal("origin", record.UpstreamRemote);
            },
            record =>
            {
                Assert.Equal("refs/remotes/upstream/main", record.FullName);
                Assert.Equal(Sha256Revision, record.Revision);
                Assert.Equal("commit", record.ObjectType);
                Assert.Null(record.SymbolicTarget);
                Assert.Null(record.UpstreamRemote);
            },
            record =>
            {
                Assert.Equal("refs/remotes/origin/HEAD", record.FullName);
                Assert.Equal("refs/remotes/origin/main", record.SymbolicTarget);
            });
    }

    /// <summary>
    ///     Verifies that an empty reviewed target stream produces an empty record collection.
    /// </summary>
    [Fact]
    public void ParseTargetRecordsAcceptsEmptyOutput()
    {
        var result = GitComparisonOutputParser.ParseTargetRecords(Success(string.Empty));

        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<GitComparisonTargetRecord>>(result.Data));
    }

    /// <summary>
    ///     Verifies duplicate otherwise-valid full refs are rejected before target classification.
    /// </summary>
    [Fact]
    public void ParseTargetRecordsRejectsDuplicateFullReferences()
    {
        var standardOutput =
            $"refs/heads/topic\0{Sha1Revision}\0commit\0\0\0\n" +
            $"refs/heads/topic\0{OtherSha1Revision}\0commit\0\0origin\0\n";

        var result = GitComparisonOutputParser.ParseTargetRecords(Success(standardOutput));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that known noncommit objects remain available for capability filtering.
    /// </summary>
    /// <param name="objectType">The Git object type.</param>
    [Theory]
    [InlineData("blob")]
    [InlineData("tree")]
    [InlineData("tag")]
    public void ParseTargetRecordsAcceptsKnownNoncommitObjectTypes(string objectType)
    {
        var output = $"refs/example/item\0{Sha1Revision}\0{objectType}\0\0\0\n";

        var result = GitComparisonOutputParser.ParseTargetRecords(Success(output));

        Assert.True(result.IsSuccess);
        Assert.Equal(objectType, Assert.Single(result.Data!).ObjectType);
    }

    /// <summary>
    ///     Verifies well-formed overlong references remain available for capability classification.
    /// </summary>
    [Fact]
    public void ParseTargetRecordsPreservesOverlongReferenceForDiscoveryClassification()
    {
        var fullName = $"refs/heads/{new string('a', ComparisonLimits.MaximumRefScalars)}";
        var output = $"{fullName}\0{Sha1Revision}\0commit\0\0\0\n";

        var result = GitComparisonOutputParser.ParseTargetRecords(Success(output));

        Assert.True(result.IsSuccess);
        Assert.Equal(fullName, Assert.Single(result.Data!).FullName);
    }

    /// <summary>
    ///     Verifies that malformed target field shapes and values are rejected.
    /// </summary>
    /// <param name="standardOutput">The malformed target output.</param>
    [Theory]
    [InlineData("refs/heads/main\0revision\0commit\0\0\0\n")]
    [InlineData("refs/heads/main\00123456789ABCDEF0123456789ABCDEF01234567\0commit\0\0\0\n")]
    [InlineData("refs/heads/main\0")]
    [InlineData("refs/heads/main\0" + Sha1Revision + "\0commit\0\0\n")]
    [InlineData("refs/heads/main\0" + Sha1Revision + "\0commit\0\0\0extra\0\n")]
    [InlineData("refs/heads/main\0" + Sha1Revision + "\0unknown\0\0\0\n")]
    [InlineData("refs/heads/.hidden\0" + Sha1Revision + "\0commit\0\0\0\n")]
    [InlineData("refs/heads/topic.lock\0" + Sha1Revision + "\0commit\0\0\0\n")]
    [InlineData("refs/heads/main\0" + Sha1Revision + "\0commit\0\0\0")]
    [InlineData("refs/heads/main\0" + Sha1Revision + "\0commit\0\0\0\n\n")]
    public void ParseTargetRecordsRejectsMalformedOutput(string standardOutput)
    {
        var result = GitComparisonOutputParser.ParseTargetRecords(Success(standardOutput));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies zero, one, and several full merge-base revisions.
    /// </summary>
    /// <param name="standardOutput">The merge-base output.</param>
    /// <param name="expectedCount">The expected number of revisions.</param>
    [Theory]
    [InlineData("", 0)]
    [InlineData(Sha1Revision + "\n", 1)]
    [InlineData(Sha256Revision, 1)]
    [InlineData(Sha1Revision + "\n" + OtherSha1Revision + "\n", 2)]
    public void ParseMergeBasesAcceptsReviewedCardinalities(
        string standardOutput,
        int expectedCount)
    {
        var result = GitComparisonOutputParser.ParseMergeBases(Success(standardOutput));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCount, result.Data!.Count);
    }

    /// <summary>
    ///     Verifies that merge-base output requires complete lowercase revisions.
    /// </summary>
    /// <param name="standardOutput">The malformed merge-base output.</param>
    [Theory]
    [InlineData("01234567\n")]
    [InlineData("0123456789ABCDEF0123456789ABCDEF01234567\n")]
    [InlineData(Sha1Revision + "\n\n")]
    [InlineData(Sha1Revision + "\r")]
    [InlineData(Sha1Revision + "\ninvalid\n")]
    public void ParseMergeBasesRejectsMalformedOutput(string standardOutput)
    {
        var result = GitComparisonOutputParser.ParseMergeBases(Success(standardOutput));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies that bounded target-only and current-work counts retain their orientation.
    /// </summary>
    /// <param name="standardOutput">The reviewed count output.</param>
    /// <param name="expectedTargetOnly">The expected left-side count.</param>
    /// <param name="expectedCurrentWork">The expected right-side count.</param>
    [Theory]
    [InlineData("0\t0\n", 0, 0)]
    [InlineData("17\t29\n", 17, 29)]
    [InlineData("2147483647\t2147483647", int.MaxValue, int.MaxValue)]
    public void ParseCommitCountsAcceptsBoundedNonNegativeIntegers(
        string standardOutput,
        int expectedTargetOnly,
        int expectedCurrentWork)
    {
        var result = GitComparisonOutputParser.ParseCommitCounts(Success(standardOutput));

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedTargetOnly, result.Data.TargetOnly);
        Assert.Equal(expectedCurrentWork, result.Data.CurrentWork);
    }

    /// <summary>
    ///     Verifies that commit counts reject noncanonical, negative, and overflowing values.
    /// </summary>
    /// <param name="standardOutput">The malformed count output.</param>
    [Theory]
    [InlineData("")]
    [InlineData("1 2\n")]
    [InlineData("1\t2\t3\n")]
    [InlineData("-1\t2\n")]
    [InlineData("+1\t2\n")]
    [InlineData("01\t2\n")]
    [InlineData("2147483648\t2\n")]
    [InlineData("1\t2\n\n")]
    public void ParseCommitCountsRejectsMalformedOrOverflowingValues(string standardOutput)
    {
        var result = GitComparisonOutputParser.ParseCommitCounts(Success(standardOutput));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies all reviewed committed diff statuses, including rename lineage and submodules.
    /// </summary>
    [Fact]
    public void ParseCommittedFilesAcceptsReviewedRawRecords()
    {
        var standardOutput =
            Raw("000000", "100644", ZeroSha1Revision, Sha1Revision, "A", "added.cs") +
            Raw("100644", "100644", Sha1Revision, OtherSha1Revision, "M", "modified.cs") +
            Raw("100644", "000000", Sha1Revision, ZeroSha1Revision, "D", "deleted.cs") +
            Raw("100644", "100644", Sha1Revision, Sha1Revision, "R100", "new.cs", "old.cs") +
            Raw("100644", "120000", Sha1Revision, OtherSha1Revision, "T", "typed.cs") +
            Raw("160000", "160000", Sha1Revision, OtherSha1Revision, "M", "module");

        var result = GitComparisonOutputParser.ParseCommittedFiles(Success(standardOutput));

        Assert.True(result.IsSuccess);
        var records = result.Data!;
        Assert.Equal(6, records.Count);
        Assert.Equal("added.cs", records[0].Path);
        Assert.Null(records[0].OriginalPath);
        Assert.Equal("new.cs", records[3].Path);
        Assert.Equal("old.cs", records[3].OriginalPath);
        Assert.Equal("module", records[5].Path);
    }

    /// <summary>
    ///     Verifies a regular file executable-bit change remains a raw modification.
    /// </summary>
    [Fact]
    public void ParseCommittedFilesAcceptsExecutableBitModification()
    {
        var output = Raw(
            "100644",
            "100755",
            Sha1Revision,
            Sha1Revision,
            "M",
            "script.sh");

        var result = GitComparisonOutputParser.ParseCommittedFiles(Success(output));

        Assert.True(result.IsSuccess);
        Assert.Equal("script.sh", Assert.Single(result.Data!).Path);
    }

    /// <summary>
    ///     Verifies SHA-256 object identifiers in reviewed raw diff records.
    /// </summary>
    [Fact]
    public void ParseCommittedFilesAcceptsSha256ObjectIdentifiers()
    {
        var output = Raw(
            "100644",
            "100644",
            Sha256Revision,
            OtherSha256Revision,
            "M",
            "file.cs");

        var result = GitComparisonOutputParser.ParseCommittedFiles(Success(output));

        Assert.True(result.IsSuccess);
        Assert.Equal("file.cs", Assert.Single(result.Data!).Path);
    }

    /// <summary>
    ///     Verifies raw rename scores use Git's non-padded percentage form.
    /// </summary>
    [Fact]
    public void ParseCommittedFilesAcceptsTwoDigitRenameScore()
    {
        var output = Raw(
            "100644",
            "100644",
            Sha1Revision,
            OtherSha1Revision,
            "R50",
            "new.cs",
            "old.cs");

        var result = GitComparisonOutputParser.ParseCommittedFiles(Success(output));

        Assert.True(result.IsSuccess);
        var record = Assert.Single(result.Data!);
        Assert.Equal("new.cs", record.Path);
        Assert.Equal("old.cs", record.OriginalPath);
    }

    /// <summary>
    ///     Verifies malformed, contradictory, and unreviewed raw records are rejected.
    /// </summary>
    /// <param name="standardOutput">The malformed raw output.</param>
    [Theory]
    [InlineData(":100644 100644 01234567 89abcdef M\0file.cs\0")]
    [InlineData(":10064x 100644 " + Sha1Revision + " " + OtherSha1Revision + " M\0file.cs\0")]
    [InlineData(":100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " C100\0new.cs\0old.cs\0")]
    [InlineData(":100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " R999\0new.cs\0old.cs\0")]
    [InlineData(":100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " M\0file.cs")]
    [InlineData(":100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " R100\0new.cs\0")]
    [InlineData(":100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " A\0file.cs\0")]
    [InlineData(":100644 120000 " + Sha1Revision + " " + OtherSha1Revision + " M\0file.cs\0")]
    [InlineData(":100644 100755 " + Sha1Revision + " " + OtherSha1Revision + " T\0file.cs\0")]
    [InlineData(":100644 100644 " + Sha1Revision + " " + Sha1Revision + " M\0file.cs\0")]
    [InlineData(":000000 100644 " + ZeroSha1Revision + " " + Sha1Revision + " X\0secret-path.cs\0")]
    public void ParseCommittedFilesRejectsMalformedContradictoryOrUnreviewedRecords(
        string standardOutput)
    {
        var result = GitComparisonOutputParser.ParseCommittedFiles(Success(standardOutput));

        AssertInspectionFailure(result);
        Assert.DoesNotContain("secret-path.cs", result.Errors[0].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies ordinary, renamed, unmerged, untracked, and ignored porcelain-v2 records.
    /// </summary>
    [Fact]
    public void ParseWorkingTreeAcceptsReviewedPorcelainV2Records()
    {
        var standardOutput =
            Ordinary("M.", "N...", "staged.cs") +
            Ordinary(".M", "N...", "unstaged.cs") +
            Ordinary("MM", "N...", "overlap.cs") +
            Rename("R.", "N...", "new.cs", "old.cs") +
            Unmerged("UU", "N...", "conflicted.cs") +
            "? untracked.cs\0" +
            "! ignored.cs\0" +
            Ordinary(".M", "S.M.", "module");

        var result = GitComparisonOutputParser.ParseWorkingTree(Success(standardOutput));

        Assert.True(result.IsSuccess);
        var records = result.Data!;
        Assert.Equal(8, records.Count);
        Assert.True(records[0].IsStaged);
        Assert.False(records[0].IsUnstaged);
        Assert.False(records[1].IsStaged);
        Assert.True(records[1].IsUnstaged);
        Assert.True(records[2].IsStaged);
        Assert.True(records[2].IsUnstaged);
        Assert.Equal("new.cs", records[3].Path);
        Assert.Equal("old.cs", records[3].OriginalPath);
        Assert.True(records[4].IsConflicted);
        Assert.True(records[5].IsUntracked);
        Assert.True(records[6].IsIgnored);
        Assert.True(records[7].IsUnstaged);
    }

    /// <summary>
    ///     Verifies staged and unstaged mode changes use Git file-type families.
    /// </summary>
    [Fact]
    public void ParseWorkingTreeAcceptsModeAndTypeChanges()
    {
        var standardOutput =
            $"1 M. N... 100644 100755 100755 {Sha1Revision} {Sha1Revision} staged-chmod.sh\0" +
            $"1 .M N... 100644 100644 100755 {Sha1Revision} {Sha1Revision} unstaged-chmod.sh\0" +
            $"1 T. N... 100644 120000 120000 {Sha1Revision} {OtherSha1Revision} staged-type\0" +
            $"1 .T N... 100644 100644 120000 {Sha1Revision} {Sha1Revision} unstaged-type\0";

        var result = GitComparisonOutputParser.ParseWorkingTree(Success(standardOutput));

        Assert.True(result.IsSuccess);
        Assert.Collection(
            result.Data!,
            record => Assert.True(record.IsStaged),
            record => Assert.True(record.IsUnstaged),
            record => Assert.True(record.IsStaged),
            record => Assert.True(record.IsUnstaged));
    }

    /// <summary>
    ///     Verifies every reviewed conflict code accepts only its Git stage-presence matrix.
    /// </summary>
    [Fact]
    public void ParseWorkingTreeAcceptsReviewedUnmergedStageMatrices()
    {
        var standardOutput =
            UnmergedWithStages("DD", true, false, false, "dd.cs") +
            UnmergedWithStages("AU", false, true, false, "au.cs") +
            UnmergedWithStages("UD", true, true, false, "ud.cs") +
            UnmergedWithStages("UA", false, false, true, "ua.cs") +
            UnmergedWithStages("DU", true, false, true, "du.cs") +
            UnmergedWithStages("AA", false, true, true, "aa.cs") +
            UnmergedWithStages("UU", true, true, true, "uu.cs");

        var result = GitComparisonOutputParser.ParseWorkingTree(Success(standardOutput));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Data!.Count);
        Assert.All(result.Data, record => Assert.True(record.IsConflicted));
    }

    /// <summary>
    ///     Verifies SHA-256 object identifiers in reviewed porcelain-v2 records.
    /// </summary>
    [Fact]
    public void ParseWorkingTreeAcceptsSha256ObjectIdentifiers()
    {
        var output =
            $"1 M. N... 100644 100644 100644 {Sha256Revision} {OtherSha256Revision} file.cs\0";

        var result = GitComparisonOutputParser.ParseWorkingTree(Success(output));

        Assert.True(result.IsSuccess);
        Assert.Equal("file.cs", Assert.Single(result.Data!).Path);
    }

    /// <summary>
    ///     Verifies porcelain-v2 rename scores use Git's non-padded percentage form.
    /// </summary>
    [Fact]
    public void ParseWorkingTreeAcceptsTwoDigitRenameScore()
    {
        var output =
            $"2 R. N... 100644 100644 100644 {Sha1Revision} {OtherSha1Revision} R50 new.cs\0old.cs\0";

        var result = GitComparisonOutputParser.ParseWorkingTree(Success(output));

        Assert.True(result.IsSuccess);
        var record = Assert.Single(result.Data!);
        Assert.Equal("new.cs", record.Path);
        Assert.Equal("old.cs", record.OriginalPath);
    }

    /// <summary>
    ///     Verifies malformed status, submodule, mode, object, and record shapes are rejected.
    /// </summary>
    /// <param name="standardOutput">The malformed porcelain-v2 output.</param>
    [Theory]
    [InlineData("1 .. N... 100644 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 .A N... 100644 100644 100644 " + Sha1Revision + " " + Sha1Revision + " file.cs\0")]
    [InlineData("1 DM N... 100644 000000 100644 " + Sha1Revision + " " + ZeroSha1Revision + " file.cs\0")]
    [InlineData("1 .M N... 100644 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 M. N... 100644 120000 120000 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 T. N... 100644 100755 100755 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 .M S.M. 100644 100644 100644 " + Sha1Revision + " " + Sha1Revision + " module\0")]
    [InlineData("1 .M N... 160000 160000 160000 " + Sha1Revision + " " + Sha1Revision + " module\0")]
    [InlineData("1 A. N... 000000 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 D. N... 100644 000000 000000 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 XY N... 100644 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 M. invalid 100644 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 M. N... 10064x 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " file.cs\0")]
    [InlineData("1 M. N... 100644 100644 100644 invalid " + OtherSha1Revision + " file.cs\0")]
    [InlineData("2 M. N... 100644 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " R100 new.cs\0old.cs\0")]
    [InlineData("2 R. N... 100644 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " C100 new.cs\0old.cs\0")]
    [InlineData("2 R. N... 100644 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " R100 new.cs\0")]
    [InlineData("u M. N... 100644 100644 100644 100644 " + Sha1Revision + " " + OtherSha1Revision + " " + Sha1Revision + " file.cs\0")]
    [InlineData("# branch.head main\0")]
    [InlineData("? untracked.cs")]
    public void ParseWorkingTreeRejectsMalformedAndUnreviewedRecords(string standardOutput)
    {
        var result = GitComparisonOutputParser.ParseWorkingTree(Success(standardOutput));

        AssertInspectionFailure(result);
    }

    /// <summary>
    ///     Verifies unmerged records reject contradictory stages and zero-mode object identifiers.
    /// </summary>
    [Fact]
    public void ParseWorkingTreeRejectsContradictoryUnmergedStageShapes()
    {
        var wrongMatrix = UnmergedWithStages("DD", true, true, false, "secret-dd.cs");
        var mismatchedZero =
            $"u AU N... 000000 100644 000000 100644 {Sha1Revision} {OtherSha1Revision} {ZeroSha1Revision} secret-au.cs\0";

        var wrongMatrixResult = GitComparisonOutputParser.ParseWorkingTree(Success(wrongMatrix));
        var mismatchedZeroResult = GitComparisonOutputParser.ParseWorkingTree(Success(mismatchedZero));

        AssertInspectionFailure(wrongMatrixResult);
        AssertInspectionFailure(mismatchedZeroResult);
        Assert.DoesNotContain(
            "secret",
            wrongMatrixResult.Errors[0].Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "secret",
            mismatchedZeroResult.Errors[0].Message,
            StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies every comparison parser rejects failed and noisy Git invocations safely.
    /// </summary>
    [Fact]
    public void ParsersRejectNonzeroExitAndUnexpectedStandardError()
    {
        var failed = new GitCommandOutput(1, "malicious-output", "malicious-error");
        var noisy = new GitCommandOutput(0, string.Empty, "unexpected warning");

        AssertInspectionFailure(GitComparisonOutputParser.ParseTargetRecords(failed));
        AssertInspectionFailure(GitComparisonOutputParser.ParseMergeBases(failed));
        AssertInspectionFailure(GitComparisonOutputParser.ParseCommitCounts(failed));
        AssertInspectionFailure(GitComparisonOutputParser.ParseCommittedFiles(failed));
        AssertInspectionFailure(GitComparisonOutputParser.ParseWorkingTree(failed));
        AssertInspectionFailure(GitComparisonOutputParser.ParseTargetRecords(noisy));
        AssertInspectionFailure(GitComparisonOutputParser.ParseMergeBases(noisy));
        AssertInspectionFailure(GitComparisonOutputParser.ParseCommitCounts(noisy));
        AssertInspectionFailure(GitComparisonOutputParser.ParseCommittedFiles(noisy));
        AssertInspectionFailure(GitComparisonOutputParser.ParseWorkingTree(noisy));
    }

    /// <summary>
    ///     Verifies unpaired UTF-16 is rejected without disclosing captured fields.
    /// </summary>
    [Fact]
    public void ParsersRejectInvalidUtf16WithoutDisclosingOutput()
    {
        var invalidPath = $"? secret-\ud800-path.cs\0";

        var result = GitComparisonOutputParser.ParseWorkingTree(Success(invalidPath));

        AssertInspectionFailure(result);
        Assert.Equal("Git comparison inspection failed.", result.Errors[0].Message);
        Assert.DoesNotContain("secret", result.Errors[0].Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies fact and diagnostic byte ceilings are enforced independently.
    /// </summary>
    [Fact]
    public void ParsersRejectOversizedFactOrDiagnosticStreams()
    {
        var oversizedFacts = new string('x', ComparisonLimits.MaximumFactOutputBytes + 1);
        var oversizedDiagnostics = new string('x', ComparisonLimits.MaximumDiagnosticBytes + 1);

        AssertInspectionFailure(
            GitComparisonOutputParser.ParseTargetRecords(Success(oversizedFacts)));
        AssertInspectionFailure(
            GitComparisonOutputParser.ParseTargetRecords(
                new GitCommandOutput(0, string.Empty, oversizedDiagnostics)));
    }

    /// <summary>
    ///     Verifies exactly 8 MiB of reviewed committed-file records produces the complete expected fact count.
    /// </summary>
    [Fact]
    public void ParseCommittedFilesAtExactFactLimitReturnsAllRecords()
    {
        const int recordCount = 8192;
        const int pathLength = 924;
        var standardOutput = CreateRawRecords(recordCount, pathLength);

        Assert.Equal(8 * 1024 * 1024, Encoding.UTF8.GetByteCount(standardOutput));
        var result = GitComparisonOutputParser.ParseCommittedFiles(Success(standardOutput));

        Assert.True(result.IsSuccess);
        Assert.Equal(recordCount, result.Data!.Count);
        Assert.Equal("p0000" + new string('x', pathLength - 5), result.Data[0].Path);
        Assert.Equal("p8191" + new string('x', pathLength - 5), result.Data[^1].Path);
    }

    private static GitCommandOutput Success(string standardOutput) =>
        new(0, standardOutput, string.Empty);

    private static string Raw(
        string oldMode,
        string newMode,
        string oldRevision,
        string newRevision,
        string status,
        string path,
        string? originalPath = null) =>
        originalPath is null
            ? $":{oldMode} {newMode} {oldRevision} {newRevision} {status}\0{path}\0"
            : $":{oldMode} {newMode} {oldRevision} {newRevision} {status}\0{originalPath}\0{path}\0";

    private static string CreateRawRecords(int recordCount, int pathLength)
    {
        var records = new StringBuilder(recordCount * 1024);
        for (var index = 0; index < recordCount; index++)
        {
            var path = $"p{index:D4}" + new string('x', pathLength - 5);
            records.Append(Raw("100644", "100644", Sha1Revision, OtherSha1Revision, "M", path));
        }

        return records.ToString();
    }

    private static string Ordinary(string status, string submodule, string path)
    {
        var headRevision = status[0] == 'A' ? ZeroSha1Revision : Sha1Revision;
        var indexRevision = status[0] switch
        {
            'D' => ZeroSha1Revision,
            '.' => headRevision,
            _ => OtherSha1Revision,
        };
        var mode = submodule[0] == 'S' ? "160000" : "100644";

        return $"1 {status} {submodule} {mode} {mode} {mode} {headRevision} {indexRevision} {path}\0";
    }

    private static string Rename(
        string status,
        string submodule,
        string path,
        string originalPath) =>
        $"2 {status} {submodule} 100644 100644 100644 {Sha1Revision} {OtherSha1Revision} R100 {path}\0{originalPath}\0";

    private static string Unmerged(string status, string submodule, string path) =>
        $"u {status} {submodule} 100644 100644 100644 100644 {Sha1Revision} {OtherSha1Revision} {Sha1Revision} {path}\0";

    private static string UnmergedWithStages(
        string status,
        bool stageOne,
        bool stageTwo,
        bool stageThree,
        string path) =>
        $"u {status} N... " +
        $"{Mode(stageOne)} {Mode(stageTwo)} {Mode(stageThree)} 100644 " +
        $"{ObjectId(stageOne, Sha1Revision)} {ObjectId(stageTwo, OtherSha1Revision)} {ObjectId(stageThree, Sha1Revision)} " +
        $"{path}\0";

    private static string Mode(bool isPresent) =>
        isPresent ? "100644" : "000000";

    private static string ObjectId(bool isPresent, string revision) =>
        isPresent ? revision : ZeroSha1Revision;

    private static void AssertInspectionFailure(Result result)
    {
        var error = Assert.Single(result.Errors);
        Assert.Equal(ErrorType.ExternalDependencyFailure, error.Type);
        Assert.Equal(ComparisonErrorCode.InspectionFailed, error.Code);
        Assert.Equal("Git comparison inspection failed.", error.Message);
    }
}
