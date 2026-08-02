using System.Globalization;
using System.Security.Cryptography;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Hashing.Services;
using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Core.Comparisons.Services;

/// <summary>
///     Provides canonical SHA-256 fingerprints for comparison facts.
/// </summary>
internal static class ComparisonFingerprint
{
    /// <summary>
    ///     Creates a target-set token bound to one repository and exact paging query.
    /// </summary>
    /// <param name="canonicalPath">The canonical repository path. Cannot be <see langword="null" />.</param>
    /// <param name="query">
    ///     The exact target query, or <see langword="null" /> when the query was omitted.
    /// </param>
    /// <param name="targets">The complete supported target set. Cannot be <see langword="null" />.</param>
    /// <param name="unsupportedTargetCount">The number of unsupported discovered targets.</param>
    /// <returns>A deterministic 64-character lowercase SHA-256 token.</returns>
    internal static string CreateTargetSetToken(
        string canonicalPath,
        string? query,
        IReadOnlyList<ComparisonTargetDescriptor> targets,
        int unsupportedTargetCount)
    {
        ArgumentNullException.ThrowIfNull(canonicalPath);
        ArgumentNullException.ThrowIfNull(targets);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        CanonicalFieldHasher.AppendField(hash, "comparison.target-set.v1");
        CanonicalFieldHasher.AppendField(hash, "repository.canonical-path");
        CanonicalFieldHasher.AppendField(hash, canonicalPath);
        CanonicalFieldHasher.AppendNullableField(hash, "query", query);
        CanonicalFieldHasher.AppendField(hash, "unsupported-target-count");
        CanonicalFieldHasher.AppendField(hash, unsupportedTargetCount.ToString(CultureInfo.InvariantCulture));

        var orderedTargets = targets
            .OrderBy(target => target.FullName, StringComparer.Ordinal)
            .ThenBy(target => target.Kind)
            .ThenBy(target => target.Name, StringComparer.Ordinal)
            .ThenBy(target => target.Revision, StringComparer.Ordinal)
            .ToArray();

        CanonicalFieldHasher.AppendField(hash, "target-count");
        CanonicalFieldHasher.AppendField(hash, orderedTargets.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var target in orderedTargets)
        {
            CanonicalFieldHasher.AppendField(hash, "target");
            CanonicalFieldHasher.AppendField(hash, target.Kind == ComparisonTargetKind.Local ? "local" : "remote-tracking");
            CanonicalFieldHasher.AppendField(hash, target.Name);
            CanonicalFieldHasher.AppendField(hash, target.FullName);
            CanonicalFieldHasher.AppendField(hash, target.Revision);
        }

        return CanonicalFieldHasher.Complete(hash);
    }

    /// <summary>
    ///     Creates a freshness token from repository, target, target-set, and normalized working-tree facts.
    /// </summary>
    /// <param name="repository">The inspected repository. Cannot be <see langword="null" />.</param>
    /// <param name="target">The selected target. Cannot be <see langword="null" />.</param>
    /// <param name="targetSetToken">The complete target-set token. Cannot be <see langword="null" />.</param>
    /// <param name="workingTree">The normalized working-tree records. Cannot be <see langword="null" />.</param>
    /// <returns>A deterministic 64-character lowercase SHA-256 token.</returns>
    internal static string CreateFreshnessToken(
        RepositoryDescriptor repository,
        ComparisonTargetDescriptor target,
        string targetSetToken,
        IReadOnlyList<GitWorkingTreeRecord> workingTree)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(targetSetToken);
        ArgumentNullException.ThrowIfNull(workingTree);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        CanonicalFieldHasher.AppendField(hash, "comparison.freshness.v1");
        CanonicalFieldHasher.AppendField(hash, "repository.canonical-path");
        CanonicalFieldHasher.AppendField(hash, repository.CanonicalPath);
        AppendHead(hash, repository.Head);
        CanonicalFieldHasher.AppendField(hash, "target");
        CanonicalFieldHasher.AppendField(hash, target.FullName);
        CanonicalFieldHasher.AppendField(hash, target.Revision);
        CanonicalFieldHasher.AppendField(hash, "target-set-token");
        CanonicalFieldHasher.AppendField(hash, targetSetToken);

        var orderedRecords = workingTree
            .OrderBy(record => record.Path, StringComparer.Ordinal)
            .ThenBy(record => record.OriginalPath is null ? 0 : 1)
            .ThenBy(record => record.OriginalPath, StringComparer.Ordinal)
            .ThenBy(record => record.IsStaged)
            .ThenBy(record => record.IsUnstaged)
            .ThenBy(record => record.IsUntracked)
            .ThenBy(record => record.IsConflicted)
            .ThenBy(record => record.IsIgnored)
            .ToArray();

        CanonicalFieldHasher.AppendField(hash, "working-tree-record-count");
        CanonicalFieldHasher.AppendField(hash, orderedRecords.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var record in orderedRecords)
        {
            CanonicalFieldHasher.AppendField(hash, "working-tree-record");
            CanonicalFieldHasher.AppendField(hash, record.Path);
            CanonicalFieldHasher.AppendNullableField(hash, "original-path", record.OriginalPath);
            CanonicalFieldHasher.AppendBooleanField(hash, "staged", record.IsStaged);
            CanonicalFieldHasher.AppendBooleanField(hash, "unstaged", record.IsUnstaged);
            CanonicalFieldHasher.AppendBooleanField(hash, "untracked", record.IsUntracked);
            CanonicalFieldHasher.AppendBooleanField(hash, "conflicted", record.IsConflicted);
            CanonicalFieldHasher.AppendBooleanField(hash, "ignored", record.IsIgnored);
        }

        return CanonicalFieldHasher.Complete(hash);
    }

    private static void AppendHead(
        IncrementalHash hash,
        RepositoryHead head)
    {
        ArgumentNullException.ThrowIfNull(head);

        switch (head)
        {
            case BranchRepositoryHead branch:
                CanonicalFieldHasher.AppendField(hash, "head.branch");
                CanonicalFieldHasher.AppendField(hash, branch.Name);
                CanonicalFieldHasher.AppendField(hash, branch.Revision);
                break;
            case DetachedRepositoryHead detached:
                CanonicalFieldHasher.AppendField(hash, "head.detached");
                CanonicalFieldHasher.AppendField(hash, detached.Revision);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(head),
                    head.GetType(),
                    "The repository HEAD kind is not supported.");
        }
    }
}
