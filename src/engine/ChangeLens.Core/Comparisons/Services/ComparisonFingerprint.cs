using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Models;

namespace ChangeLens.Core.Comparisons.Services;

/// <summary>
///     Provides canonical SHA-256 fingerprints for comparison facts.
/// </summary>
internal static class ComparisonFingerprint
{
    /// <summary>
    ///     Rejects unpaired UTF-16 surrogates instead of replacing them in fingerprint fields.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

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
        AppendField(hash, "comparison.target-set.v1");
        AppendField(hash, "repository.canonical-path");
        AppendField(hash, canonicalPath);
        AppendNullableField(hash, "query", query);
        AppendField(hash, "unsupported-target-count");
        AppendField(hash, unsupportedTargetCount.ToString(CultureInfo.InvariantCulture));

        var orderedTargets = targets
            .OrderBy(target => target.FullName, StringComparer.Ordinal)
            .ThenBy(target => target.Kind)
            .ThenBy(target => target.Name, StringComparer.Ordinal)
            .ThenBy(target => target.Revision, StringComparer.Ordinal)
            .ToArray();

        AppendField(hash, "target-count");
        AppendField(hash, orderedTargets.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var target in orderedTargets)
        {
            AppendField(hash, "target");
            AppendField(hash, target.Kind == ComparisonTargetKind.Local ? "local" : "remote-tracking");
            AppendField(hash, target.Name);
            AppendField(hash, target.FullName);
            AppendField(hash, target.Revision);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
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
        AppendField(hash, "comparison.freshness.v1");
        AppendField(hash, "repository.canonical-path");
        AppendField(hash, repository.CanonicalPath);
        AppendHead(hash, repository.Head);
        AppendField(hash, "target");
        AppendField(hash, target.FullName);
        AppendField(hash, target.Revision);
        AppendField(hash, "target-set-token");
        AppendField(hash, targetSetToken);

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

        AppendField(hash, "working-tree-record-count");
        AppendField(hash, orderedRecords.Length.ToString(CultureInfo.InvariantCulture));

        foreach (var record in orderedRecords)
        {
            AppendField(hash, "working-tree-record");
            AppendField(hash, record.Path);
            AppendNullableField(hash, "original-path", record.OriginalPath);
            AppendBooleanField(hash, "staged", record.IsStaged);
            AppendBooleanField(hash, "unstaged", record.IsUnstaged);
            AppendBooleanField(hash, "untracked", record.IsUntracked);
            AppendBooleanField(hash, "conflicted", record.IsConflicted);
            AppendBooleanField(hash, "ignored", record.IsIgnored);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    private static void AppendHead(
        IncrementalHash hash,
        RepositoryHead head)
    {
        ArgumentNullException.ThrowIfNull(head);

        switch (head)
        {
            case BranchRepositoryHead branch:
                AppendField(hash, "head.branch");
                AppendField(hash, branch.Name);
                AppendField(hash, branch.Revision);
                break;
            case DetachedRepositoryHead detached:
                AppendField(hash, "head.detached");
                AppendField(hash, detached.Revision);
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(head),
                    head.GetType(),
                    "The repository HEAD kind is not supported.");
        }
    }

    private static void AppendNullableField(
        IncrementalHash hash,
        string marker,
        string? value)
    {
        AppendField(hash, marker);

        if (value is null)
        {
            AppendField(hash, "absent");
            return;
        }

        AppendField(hash, "present");
        AppendField(hash, value);
    }

    private static void AppendBooleanField(
        IncrementalHash hash,
        string marker,
        bool value)
    {
        AppendField(hash, marker);
        AppendField(hash, value ? "true" : "false");
    }

    /// <summary>
    ///     Appends one UTF-8 field after its four-byte big-endian byte length.
    /// </summary>
    /// <param name="hash">The incremental SHA-256 hash that receives the canonical field.</param>
    /// <param name="value">The field value to encode.</param>
    private static void AppendField(
        IncrementalHash hash,
        string value)
    {
        var bytes = StrictUtf8.GetBytes(value);
        Span<byte> length = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(length, bytes.Length);
        hash.AppendData(length);
        hash.AppendData(bytes);
    }
}
