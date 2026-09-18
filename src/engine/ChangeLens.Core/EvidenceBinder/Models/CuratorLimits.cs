namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents the mechanical limits in the closed curator contract.
/// </summary>
/// <param name="MaximumTracks">The maximum tracks.</param>
/// <param name="MaximumParticipantsPerTrack">The maximum participants per track.</param>
/// <param name="MaximumRelationshipsPerTrack">The maximum relationships per track.</param>
/// <param name="MaximumItemsPerTrack">The maximum ordered steps or purposes per track.</param>
/// <param name="MaximumStatementCharacters">The maximum statement size.</param>
/// <param name="IdFormat">The allowed identifier format.</param>
public sealed record CuratorLimits(
    int MaximumTracks,
    int MaximumParticipantsPerTrack,
    int MaximumRelationshipsPerTrack,
    int MaximumItemsPerTrack,
    int MaximumStatementCharacters,
    string IdFormat);
