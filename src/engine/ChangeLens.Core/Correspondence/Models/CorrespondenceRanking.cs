namespace ChangeLens.Core.Correspondence.Models;

/// <summary>
///     Represents the unchanged files ranked for attention against one change anatomy.
/// </summary>
/// <param name="Candidates">The returned candidates in rank order. Cannot be <see langword="null" />.</param>
/// <param name="Diagnostics">The indexing, query, and history diagnostics. Cannot be <see langword="null" />.</param>
public sealed record CorrespondenceRanking(
    IReadOnlyList<CorrespondenceCandidate> Candidates,
    CorrespondenceDiagnostics Diagnostics);
