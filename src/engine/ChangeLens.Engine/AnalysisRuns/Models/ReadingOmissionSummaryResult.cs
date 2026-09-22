namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one group of evidence publication did not quote.
/// </summary>
/// <param name="SourceKind">The omission source-kind wire value.</param>
/// <param name="Reason">The producer reason for the group.</param>
/// <param name="TotalCount">The total number of omitted items in the group.</param>
/// <param name="SampleCount">The number of sampled items.</param>
/// <param name="ResolvedSampleCount">The number of sampled items publication resolved to an exact path.</param>
internal sealed record ReadingOmissionSummaryResult(string SourceKind, string Reason, int TotalCount, int SampleCount, int ResolvedSampleCount);
