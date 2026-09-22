namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one published statement.
/// </summary>
/// <param name="ClaimId">The stable claim identifier.</param>
/// <param name="Text">The statement text.</param>
/// <param name="Trust">The trust wire value.</param>
/// <param name="EvidenceNodeIds">The disclosed evidence node identifiers the statement cites.</param>
internal sealed record ReadingStatementResult(string ClaimId, string Text, string Trust, IReadOnlyList<string> EvidenceNodeIds);
