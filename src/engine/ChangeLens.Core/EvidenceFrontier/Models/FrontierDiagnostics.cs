namespace ChangeLens.Core.EvidenceFrontier.Models;

/// <summary>
///     Represents counts for the complete frontier before its returned-entry cap is applied.
/// </summary>
/// <param name="CandidateCount">The number of ranked candidates.</param>
/// <param name="GraphNodeCount">The number of evidence graph nodes.</param>
/// <param name="DisclosedNodeCount">The number of nodes disclosed by context policy.</param>
/// <param name="BoundNodeCount">The number of evidence nodes retained by the binder.</param>
/// <param name="UsedNodeCount">The number of bound nodes present in the supplied used-node set.</param>
/// <param name="EntryCount">The number of entries returned after the cap.</param>
/// <param name="TotalEntryCount">The number of entries before the cap.</param>
/// <param name="TruncatedEntryCount">The number of entries removed by the cap.</param>
/// <param name="CandidateNotQuotedCount">The pre-cap count of candidate-not-quoted entries.</param>
/// <param name="PolicyExcludedCount">The pre-cap count of policy-excluded entries.</param>
/// <param name="BudgetDroppedCount">The pre-cap count of budget-dropped entries.</param>
/// <param name="BoundNotUsedCount">The pre-cap count of bound-not-used entries.</param>
/// <param name="PostCheck">Whether a used-node set was supplied.</param>
/// <param name="OmissionsByKind">The pre-cap counts keyed by omission kind. Cannot be <see langword="null" />.</param>
public sealed record FrontierDiagnostics(
    int CandidateCount,
    int GraphNodeCount,
    int DisclosedNodeCount,
    int BoundNodeCount,
    int UsedNodeCount,
    int EntryCount,
    int TotalEntryCount,
    int TruncatedEntryCount,
    int CandidateNotQuotedCount,
    int PolicyExcludedCount,
    int BudgetDroppedCount,
    int BoundNotUsedCount,
    bool PostCheck,
    IReadOnlyDictionary<string, int> OmissionsByKind);
