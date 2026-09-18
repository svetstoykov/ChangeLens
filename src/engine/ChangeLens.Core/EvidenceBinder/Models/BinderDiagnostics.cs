namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents assembly sizes, coverage counters, policy diagnostics, and budget-ladder decisions.
/// </summary>
/// <param name="BinderCharacterCount">The post-assembly binder payload size.</param>
/// <param name="PayloadCharacterCount">The curator payload size at initial assembly.</param>
/// <param name="TokenEstimate">The character-based token estimate at 3.25 characters per token.</param>
/// <param name="TargetCharacters">The ordinary ladder target.</param>
/// <param name="BudgetCharacters">The hard binder cap.</param>
/// <param name="BudgetBinding">Whether any ladder step fired.</param>
/// <param name="LadderStepsApplied">The ordered ladder steps that fired.</param>
/// <param name="EvidenceCount">The retained evidence count.</param>
/// <param name="RedactedNodeCount">The retained redacted-node count.</param>
/// <param name="PolicyExcludedNodeCount">The policy-excluded node count.</param>
/// <param name="MatchEdgeCount">The retained engine-only edge count.</param>
/// <param name="ChangedFileCount">The retained changed-file entry count.</param>
/// <param name="ChangedFilesWithoutEvidenceCount">The changed-file entries without retained evidence.</param>
/// <param name="OrientationPathCount">The retained orientation path count.</param>
/// <param name="PolicyInvariantViolationNodeIds">The node ids dropped for policy invariant violations.</param>
public sealed record BinderDiagnostics(
    int BinderCharacterCount,
    int PayloadCharacterCount,
    int TokenEstimate,
    int TargetCharacters,
    int BudgetCharacters,
    bool BudgetBinding,
    IReadOnlyList<string> LadderStepsApplied,
    int EvidenceCount,
    int RedactedNodeCount,
    int PolicyExcludedNodeCount,
    int MatchEdgeCount,
    int ChangedFileCount,
    int ChangedFilesWithoutEvidenceCount,
    int OrientationPathCount,
    IReadOnlyList<string> PolicyInvariantViolationNodeIds)
{
    /// <summary>
    ///     Gets the post-assembly binder character count using the prototype's established name.
    /// </summary>
    public int CharacterCount => this.BinderCharacterCount;
}
