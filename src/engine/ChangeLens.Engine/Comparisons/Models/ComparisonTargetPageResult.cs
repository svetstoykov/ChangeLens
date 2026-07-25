namespace ChangeLens.Engine.Comparisons.Models;

/// <summary>
///     Represents one bounded page of comparison targets.
/// </summary>
/// <param name="Targets">The emitted targets in Core order.</param>
/// <param name="SuggestedTarget">The conservative suggested target, or <see langword="null" />.</param>
/// <param name="NextCursor">The last emitted full ref when another supported target remains.</param>
/// <param name="TargetSetToken">The deterministic target-set token.</param>
/// <param name="UnsupportedTargetCount">The number of unsupported discovered or protocol-sized targets.</param>
internal sealed record ComparisonTargetPageResult(
    IReadOnlyList<ComparisonTargetResult> Targets,
    ComparisonTargetResult? SuggestedTarget,
    string? NextCursor,
    string TargetSetToken,
    int UnsupportedTargetCount);
