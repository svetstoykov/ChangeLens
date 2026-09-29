namespace ChangeLens.Core.FindingValidation.Constants;

/// <summary>
///     Defines the stable finding-validation scope, reasons, and limits.
/// </summary>
public static class FindingValidationConstants
{
    /// <summary>
    ///     Gets the scope for every finding removal.
    /// </summary>
    public const string FindingScope = "finding";

    /// <summary>
    ///     Gets the reason for findings beyond the incoming-position limit.
    /// </summary>
    public const string OverCapReason = "overCap";

    /// <summary>
    ///     Gets the reason for a finding with invalid fields or an invalid identifier.
    /// </summary>
    public const string InvalidFieldReason = "invalidField";

    /// <summary>
    ///     Gets the reason for a finding that cites undisclosed evidence.
    /// </summary>
    public const string UndisclosedEvidenceReason = "undisclosedEvidence";

    /// <summary>
    ///     Gets the reason for a finding without a changed-file citation.
    /// </summary>
    public const string NoChangedFileCitationReason = "noChangedFileCitation";

    /// <summary>
    ///     Gets the reason for a finding with an invalid source anchor.
    /// </summary>
    public const string AnchorMismatchReason = "anchorMismatch";

    /// <summary>
    ///     Gets the reason for a source anchor that occurs more than once.
    /// </summary>
    public const string AnchorAmbiguousReason = "anchorAmbiguous";

    /// <summary>
    ///     Gets the maximum number of findings considered for publication.
    /// </summary>
    public const int MaximumFindings = 10;

    /// <summary>
    ///     Gets the maximum finding-title length in characters.
    /// </summary>
    public const int MaximumTitleCharacters = 120;

    /// <summary>
    ///     Gets the maximum finding identifier length in characters.
    /// </summary>
    public const int MaximumIdCharacters = 120;
}
