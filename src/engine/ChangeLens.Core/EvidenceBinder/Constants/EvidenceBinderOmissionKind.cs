namespace ChangeLens.Core.EvidenceBinder.Constants;

/// <summary>
///     Defines stable omission kinds recorded by the evidence binder.
/// </summary>
public static class EvidenceBinderOmissionKind
{
    /// <summary>Policy excluded evidence nodes.</summary>
    public const string PolicyExcluded = "policyExcluded";

    /// <summary>Evidence removed by the character-budget ladder.</summary>
    public const string BudgetDropped = "budgetDropped";

    /// <summary>A matched value was withheld by context policy.</summary>
    public const string MatchedValueWithheld = "matchedValueWithheld";

    /// <summary>Developer context exceeded its configured character cap.</summary>
    public const string DeveloperContextTruncated = "developerContextTruncated";

    /// <summary>A disclosed node violated a context-policy invariant.</summary>
    public const string PolicyInvariantViolation = "policyInvariantViolation";

    /// <summary>A changed file could not be read from the frozen snapshot.</summary>
    public const string FileNotRead = "fileNotRead";

    /// <summary>A changed file has no disclosed evidence node.</summary>
    public const string NoEvidenceSelected = "noEvidenceSelected";
}
