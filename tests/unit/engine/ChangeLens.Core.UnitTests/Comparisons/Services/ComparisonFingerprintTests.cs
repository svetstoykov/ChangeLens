using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Models;
using Xunit;

namespace ChangeLens.Core.UnitTests.Comparisons.Services;

/// <summary>
///     Verifies canonical comparison target-set and freshness fingerprints.
/// </summary>
public sealed class ComparisonFingerprintTests
{
    private const string Sha1Revision = "0123456789abcdef0123456789abcdef01234567";
    private const string OtherSha1Revision = "89abcdef0123456789abcdef0123456789abcdef";

    /// <summary>
    ///     Verifies target-set tokens are deterministic lowercase SHA-256 values.
    /// </summary>
    [Fact]
    public void CreateTargetSetTokenIsDeterministicLowercaseSha256()
    {
        var targets = Targets();

        var first = ComparisonFingerprint.CreateTargetSetToken(
            "/repository",
            "main",
            targets,
            2);
        var second = ComparisonFingerprint.CreateTargetSetToken(
            "/repository",
            "main",
            targets,
            2);

        Assert.Equal(first, second);
        Assert.Equal(ComparisonLimits.FingerprintHexLength, first.Length);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    /// <summary>
    ///     Verifies the canonical four-byte big-endian length framing against an independent digest vector.
    /// </summary>
    [Fact]
    public void CreateTargetSetTokenMatchesCanonicalDigestVector()
    {
        var token = ComparisonFingerprint.CreateTargetSetToken(
            "a",
            null,
            [],
            0);

        Assert.Equal(
            "4bfb1e014776e6aa5c449159abe155eef67623629fc3d16fa1b7cac21fffc4e2",
            token);
    }

    /// <summary>
    ///     Verifies target ordering is normalized by ordinal full reference identity.
    /// </summary>
    [Fact]
    public void CreateTargetSetTokenNormalizesTargetOrder()
    {
        var targets = Targets();

        var forward = ComparisonFingerprint.CreateTargetSetToken(
            "/repository",
            null,
            targets,
            0);
        var reverse = ComparisonFingerprint.CreateTargetSetToken(
            "/repository",
            null,
            targets.Reverse().ToArray(),
            0);

        Assert.Equal(forward, reverse);
    }

    /// <summary>
    ///     Verifies every target-set paging fact contributes to the public token.
    /// </summary>
    [Fact]
    public void CreateTargetSetTokenChangesWithEveryPagingFact()
    {
        var targets = Targets();
        var baseline = ComparisonFingerprint.CreateTargetSetToken(
            "/repository",
            null,
            targets,
            0);

        var variations = new[]
        {
            ComparisonFingerprint.CreateTargetSetToken("/other", null, targets, 0),
            ComparisonFingerprint.CreateTargetSetToken("/repository", string.Empty, targets, 0),
            ComparisonFingerprint.CreateTargetSetToken("/repository", "main", targets, 0),
            ComparisonFingerprint.CreateTargetSetToken("/repository", null, targets, 1),
            ComparisonFingerprint.CreateTargetSetToken(
                "/repository",
                null,
                ReplaceFirst(
                    targets,
                    targets[0] with { Kind = ComparisonTargetKind.RemoteTracking }),
                0),
            ComparisonFingerprint.CreateTargetSetToken(
                "/repository",
                null,
                ReplaceFirst(targets, targets[0] with { Name = "renamed" }),
                0),
            ComparisonFingerprint.CreateTargetSetToken(
                "/repository",
                null,
                ReplaceFirst(targets, targets[0] with { FullName = "refs/heads/renamed" }),
                0),
            ComparisonFingerprint.CreateTargetSetToken(
                "/repository",
                null,
                ReplaceFirst(targets, targets[0] with { Revision = OtherSha1Revision }),
                0),
        };

        Assert.All(variations, token => Assert.NotEqual(baseline, token));
    }

    /// <summary>
    ///     Verifies freshness tokens normalize working-tree record order.
    /// </summary>
    [Fact]
    public void CreateFreshnessTokenNormalizesWorkingTreeOrder()
    {
        var repository = Repository();
        var target = Targets()[0];
        var records = WorkingTree();

        var forward = ComparisonFingerprint.CreateFreshnessToken(
            repository,
            target,
            "target-set-token",
            records);
        var reverse = ComparisonFingerprint.CreateFreshnessToken(
            repository,
            target,
            "target-set-token",
            records.Reverse().ToArray());

        Assert.Equal(forward, reverse);
        Assert.Equal(ComparisonLimits.FingerprintHexLength, forward.Length);
        Assert.Matches("^[0-9a-f]{64}$", forward);
    }

    /// <summary>
    ///     Verifies repository, target, target-set, paths, lineage, and categories contribute to freshness.
    /// </summary>
    [Fact]
    public void CreateFreshnessTokenChangesWithEveryFreshnessFact()
    {
        var repository = Repository();
        var target = Targets()[0];
        var records = WorkingTree();
        var baseline = ComparisonFingerprint.CreateFreshnessToken(
            repository,
            target,
            "target-set-token",
            records);

        var variations = new[]
        {
            Token(repository with { CanonicalPath = "/other" }, target, "target-set-token", records),
            Token(
                repository with
                {
                    Head = new BranchRepositoryHead("other", Sha1Revision),
                },
                target,
                "target-set-token",
                records),
            Token(
                repository with
                {
                    Head = new DetachedRepositoryHead(Sha1Revision),
                },
                target,
                "target-set-token",
                records),
            Token(
                repository with
                {
                    Head = new BranchRepositoryHead("main", OtherSha1Revision),
                },
                target,
                "target-set-token",
                records),
            Token(
                repository,
                target with { FullName = "refs/heads/other" },
                "target-set-token",
                records),
            Token(
                repository,
                target with { Revision = OtherSha1Revision },
                "target-set-token",
                records),
            Token(repository, target, "other-token", records),
            Token(
                repository,
                target,
                "target-set-token",
                ReplaceFirst(records, records[0] with { Path = "renamed.cs" })),
            Token(
                repository,
                target,
                "target-set-token",
                ReplaceFirst(records, records[0] with { OriginalPath = null })),
            Token(
                repository,
                target,
                "target-set-token",
                ReplaceFirst(records, records[0] with { IsStaged = false })),
            Token(
                repository,
                target,
                "target-set-token",
                ReplaceFirst(records, records[0] with { IsUnstaged = true })),
            Token(
                repository,
                target,
                "target-set-token",
                ReplaceFirst(records, records[0] with { IsUntracked = true })),
            Token(
                repository,
                target,
                "target-set-token",
                ReplaceFirst(records, records[0] with { IsConflicted = true })),
            Token(
                repository,
                target,
                "target-set-token",
                ReplaceFirst(records, records[0] with { IsIgnored = true })),
        };

        Assert.All(variations, token => Assert.NotEqual(baseline, token));
    }

    private static IReadOnlyList<ComparisonTargetDescriptor> Targets() =>
    [
        new(
            ComparisonTargetKind.Local,
            "feature",
            "refs/heads/feature",
            Sha1Revision),
        new(
            ComparisonTargetKind.RemoteTracking,
            "origin/main",
            "refs/remotes/origin/main",
            OtherSha1Revision),
    ];

    private static RepositoryDescriptor Repository() =>
        new(
            "repository",
            "/repository",
            new BranchRepositoryHead("main", Sha1Revision));

    private static IReadOnlyList<GitWorkingTreeRecord> WorkingTree() =>
    [
        new(
            "new.cs",
            "old.cs",
            true,
            false,
            false,
            false,
            false),
        new(
            "untracked.cs",
            null,
            false,
            false,
            true,
            false,
            false),
    ];

    private static string Token(
        RepositoryDescriptor repository,
        ComparisonTargetDescriptor target,
        string targetSetToken,
        IReadOnlyList<GitWorkingTreeRecord> workingTree) =>
        ComparisonFingerprint.CreateFreshnessToken(
            repository,
            target,
            targetSetToken,
            workingTree);

    private static IReadOnlyList<T> ReplaceFirst<T>(
        IReadOnlyList<T> values,
        T replacement) =>
        [replacement, .. values.Skip(1)];
}
