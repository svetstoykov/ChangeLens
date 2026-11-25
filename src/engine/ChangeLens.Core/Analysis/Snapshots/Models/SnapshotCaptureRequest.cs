namespace ChangeLens.Core.Analysis.Snapshots.Models;

/// <summary>
///     Represents the accepted run and revision identities required to capture a committed snapshot.
/// </summary>
/// <param name="RunId">The accepted analysis run identifier.</param>
/// <param name="CanonicalPath">The canonical repository root path. Cannot be <see langword="null" />.</param>
/// <param name="CanonicalRepositoryPathKey">The stable canonical repository path key. Cannot be <see langword="null" />.</param>
/// <param name="Target">The selected comparison target ref. Cannot be <see langword="null" />.</param>
/// <param name="TargetRevision">The accepted target revision. Cannot be <see langword="null" />.</param>
/// <param name="HeadRevision">The accepted HEAD revision. Cannot be <see langword="null" />.</param>
public sealed record SnapshotCaptureRequest(
    Guid RunId,
    string CanonicalPath,
    string CanonicalRepositoryPathKey,
    string Target,
    string TargetRevision,
    string HeadRevision);
