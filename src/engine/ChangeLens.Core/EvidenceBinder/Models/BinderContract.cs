namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents the closed relationship, shape, and limit contract for the curator.
/// </summary>
/// <param name="RelationshipKinds">The allowed relationship kinds.</param>
/// <param name="TrackShapes">The allowed track shapes.</param>
/// <param name="Limits">The mechanical curator limits.</param>
public sealed record BinderContract(IReadOnlyList<string> RelationshipKinds, IReadOnlyList<string> TrackShapes, CuratorLimits Limits);
