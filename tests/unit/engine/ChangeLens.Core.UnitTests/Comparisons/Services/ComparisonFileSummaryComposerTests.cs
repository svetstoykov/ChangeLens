using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Comparisons.Services;
using Xunit;

namespace ChangeLens.Core.UnitTests.Comparisons.Services;

/// <summary>
///     Verifies distinct comparison-file lineage composition.
/// </summary>
public sealed class ComparisonFileSummaryComposerTests
{
    private readonly ComparisonFileSummaryComposer _composer = new();

    /// <summary>
    ///     Verifies that a committed file with a local edit is one changed and one uncommitted lineage.
    /// </summary>
    [Fact]
    public void ComposeDeduplicatesCommittedAndLocalEditAtTheSamePath()
    {
        var records = new[]
        {
            Record("file.cs", isCommitted: true),
            Record("file.cs", isUnstaged: true),
        };

        var result = _composer.Compose(records);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ChangedFileTotal);
        Assert.Equal(1, result.Data.UncommittedFileTotal);
        Assert.Equal(1, result.Data.UnstagedFileCount);
    }

    /// <summary>
    ///     Verifies staged and unstaged categories overlap without inflating uncommitted lineages.
    /// </summary>
    [Fact]
    public void ComposeCountsOverlappingCategoriesWithoutSummingThem()
    {
        var records = new[]
        {
            Record("file.cs", isStaged: true, isUnstaged: true),
        };

        var result = _composer.Compose(records);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ChangedFileTotal);
        Assert.Equal(1, result.Data.UncommittedFileTotal);
        Assert.Equal(1, result.Data.StagedFileCount);
        Assert.Equal(1, result.Data.UnstagedFileCount);
        Assert.Equal(0, result.Data.UntrackedFileCount);
        Assert.Equal(0, result.Data.ConflictedFileCount);
        Assert.True(
            result.Data.StagedFileCount + result.Data.UnstagedFileCount >
            result.Data.UncommittedFileTotal);
    }

    /// <summary>
    ///     Verifies that a non-ignored untracked file is one changed and uncommitted lineage.
    /// </summary>
    [Fact]
    public void ComposeCountsUntrackedFile()
    {
        var result = _composer.Compose([Record("new.cs", isUntracked: true)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ChangedFileTotal);
        Assert.Equal(1, result.Data.UncommittedFileTotal);
        Assert.Equal(1, result.Data.UntrackedFileCount);
    }

    /// <summary>
    ///     Verifies that an unmerged file is one conflicted uncommitted lineage.
    /// </summary>
    [Fact]
    public void ComposeCountsUnmergedFile()
    {
        var result = _composer.Compose([Record("conflict.cs", isConflicted: true)]);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ChangedFileTotal);
        Assert.Equal(1, result.Data.UncommittedFileTotal);
        Assert.Equal(1, result.Data.ConflictedFileCount);
    }

    /// <summary>
    ///     Verifies rename edges join transitive committed and working-tree lineage.
    /// </summary>
    [Fact]
    public void ComposeUnionsTransitiveRenameLineage()
    {
        var records = new[]
        {
            Record("middle.cs", originalPath: "old.cs", isCommitted: true),
            Record("new.cs", originalPath: "middle.cs", isStaged: true),
        };

        var result = _composer.Compose(records);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.ChangedFileTotal);
        Assert.Equal(1, result.Data.UncommittedFileTotal);
        Assert.Equal(1, result.Data.StagedFileCount);
    }

    /// <summary>
    ///     Verifies deleted, type-changed, and submodule records each retain a distinct lineage.
    /// </summary>
    [Fact]
    public void ComposeCountsDistinctCommittedFileKinds()
    {
        var records = new[]
        {
            Record("deleted.cs", isCommitted: true),
            Record("typed.cs", isCommitted: true),
            Record("module", isCommitted: true),
        };

        var result = _composer.Compose(records);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.ChangedFileTotal);
        Assert.Equal(0, result.Data.UncommittedFileTotal);
    }

    /// <summary>
    ///     Verifies that no converted records produces an all-zero summary for ignored-only status.
    /// </summary>
    [Fact]
    public void ComposeReturnsZeroForIgnoredOnlyInputAfterFiltering()
    {
        var result = _composer.Compose([]);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            new ComparisonFileSummary(0, 0, 0, 0, 0, 0),
            result.Data);
    }

    private static ComparisonFileRecord Record(
        string path,
        string? originalPath = null,
        bool isCommitted = false,
        bool isStaged = false,
        bool isUnstaged = false,
        bool isUntracked = false,
        bool isConflicted = false) =>
        new(
            path,
            originalPath,
            isCommitted,
            isStaged,
            isUnstaged,
            isUntracked,
            isConflicted);
}
