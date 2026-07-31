namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the immutable request to durably accept a new analysis run.
/// </summary>
/// <param name="CanonicalRepositoryPathKey">The canonical repository path key. Cannot be <see langword="null" />.</param>
/// <param name="RepositoryDisplayName">The repository display name. Cannot be <see langword="null" />.</param>
/// <param name="CanonicalRepositoryPath">The canonical repository path. Cannot be <see langword="null" />.</param>
/// <param name="HeadRevision">The accepted HEAD revision. Cannot be <see langword="null" />.</param>
/// <param name="Target">The exact selected comparison target reference. Cannot be <see langword="null" />.</param>
/// <param name="TargetRevision">The accepted target revision. Cannot be <see langword="null" />.</param>
/// <param name="FreshnessToken">The accepted freshness token. Cannot be <see langword="null" />.</param>
/// <param name="Checks">The immutable accepted deterministic check selection. Cannot be <see langword="null" />.</param>
/// <param name="ChangeContext">The optional developer-supplied change context, or <see langword="null" />.</param>
/// <param name="ProcessorSessionId">The accepting processor-session identifier.</param>
/// <param name="RequestedAtUnixMilliseconds">The acceptance timestamp.</param>
public sealed record AnalysisRunAcceptance(
    string CanonicalRepositoryPathKey,
    string RepositoryDisplayName,
    string CanonicalRepositoryPath,
    string HeadRevision,
    string Target,
    string TargetRevision,
    string FreshnessToken,
    AnalysisCheckSelection Checks,
    string? ChangeContext,
    Guid ProcessorSessionId,
    long RequestedAtUnixMilliseconds);
