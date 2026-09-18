namespace ChangeLens.Core.EvidenceBinder.Constants;

/// <summary>
///     Defines the ordered character-budget ladder steps.
/// </summary>
public static class EvidenceBinderLadderStep
{
    /// <summary>Removes candidate head-of-file nodes.</summary>
    public const string CandidateHeadNode = "candidateHeadNode";

    /// <summary>Removes low-salience candidate nodes.</summary>
    public const string CandidateNodeSalience = "candidateNodeSalience";

    /// <summary>Removes sibling-path orientation entries.</summary>
    public const string Orientation = "orientation";

    /// <summary>Removes low-salience changed-file nodes.</summary>
    public const string ChangedFileNodeSalience = "changedFileNodeSalience";
}
