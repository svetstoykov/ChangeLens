namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the outcome of an analysis-start acceptance attempt.
/// </summary>
/// <param name="Kind">The start outcome kind.</param>
/// <param name="RunId">The created run identifier when <paramref name="Kind" /> is <see cref="AnalysisStartOutcomeKind.Accepted" />.</param>
/// <param name="RequestedAtUnixMilliseconds">
///     The acceptance timestamp when <paramref name="Kind" /> is
///     <see cref="AnalysisStartOutcomeKind.Accepted" />.
/// </param>
/// <param name="ActiveRunId">
///     The racing active run identifier when <paramref name="Kind" /> is
///     <see cref="AnalysisStartOutcomeKind.RejectedActive" />.
/// </param>
public sealed record AnalysisStartOutcome(
    AnalysisStartOutcomeKind Kind,
    Guid? RunId,
    long? RequestedAtUnixMilliseconds,
    Guid? ActiveRunId);
