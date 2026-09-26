namespace ChangeLens.Core.FindingValidation.Models;

/// <summary>
///     Represents the absolute source lines focused by a validated finding.
/// </summary>
/// <param name="StartLine">The one-based first focus line.</param>
/// <param name="EndLine">The one-based last focus line.</param>
public sealed record FindingFocusRange(int StartLine, int EndLine);
