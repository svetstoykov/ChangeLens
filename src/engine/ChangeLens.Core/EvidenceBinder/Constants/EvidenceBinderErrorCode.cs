namespace ChangeLens.Core.EvidenceBinder.Constants;

/// <summary>
///     Provides stable error codes for evidence binder assembly.
/// </summary>
public static class EvidenceBinderErrorCode
{
    /// <summary>
    ///     Identifies invalid binder limits or budget settings.
    /// </summary>
    public const string InvalidOptions = "evidenceBinder.invalidOptions";

    /// <summary>
    ///     Identifies a binder whose mandatory content cannot fit its configured hard cap.
    /// </summary>
    public const string BudgetExceeded = "evidenceBinder.budgetExceeded";
}
