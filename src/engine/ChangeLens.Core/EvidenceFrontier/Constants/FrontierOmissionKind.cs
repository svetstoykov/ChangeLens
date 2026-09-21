namespace ChangeLens.Core.EvidenceFrontier.Constants;

/// <summary>
///     Defines stable omission kinds reported by the evidence frontier.
/// </summary>
public static class FrontierOmissionKind
{
    /// <summary>
    ///     A ranked candidate produced no evidence graph node.
    /// </summary>
    public const string CandidateNotQuoted = "candidateNotQuoted";

    /// <summary>
    ///     A graph node was withheld by context policy.
    /// </summary>
    public const string PolicyExcluded = "policyExcluded";

    /// <summary>
    ///     A graph node was removed by the binder budget or was not admitted after disclosure.
    /// </summary>
    public const string BudgetDropped = "budgetDropped";

    /// <summary>
    ///     A disclosed and bound node was not cited by the checked model.
    /// </summary>
    public const string BoundNotUsed = "boundNotUsed";
}
