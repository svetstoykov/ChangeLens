namespace ChangeLens.Core.AnalysisRuns.Models;

/// <summary>
///     Represents the immutable strict terminal outcome of a completed analysis run.
/// </summary>
/// <param name="Kind">The strict terminal outcome kind.</param>
/// <param name="TerminalAtUnixMilliseconds">
///     The UTC Unix millisecond timestamp the terminal state committed.
/// </param>
/// <param name="LimitationCount">
///     The non-negative recorded limitation count when <paramref name="Kind" /> is
///     <see cref="AnalysisTerminalKind.CompletedWithLimitations" />; otherwise,
///     <see langword="null" />.
/// </param>
/// <param name="FailureCode">
///     The stable failure code when <paramref name="Kind" /> is
///     <see cref="AnalysisTerminalKind.Failed" />; otherwise, <see langword="null" />.
/// </param>
public sealed record AnalysisTerminalSummary(
    AnalysisTerminalKind Kind,
    long TerminalAtUnixMilliseconds,
    int? LimitationCount,
    string? FailureCode);
