namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents the protocol projection of one area participant.
/// </summary>
/// <param name="Id">The participant identifier.</param>
/// <param name="Name">The participant name.</param>
/// <param name="Role">The participant role.</param>
/// <param name="Changed">Whether the participant is part of the change.</param>
/// <param name="EvidenceNodeIds">The disclosed evidence node identifiers the participant cites.</param>
internal sealed record ReadingParticipantResult(string Id, string Name, string Role, bool Changed, IReadOnlyList<string> EvidenceNodeIds);
