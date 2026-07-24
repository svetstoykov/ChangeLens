namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Represents immutable comparison targets and their paging identity.
/// </summary>
/// <param name="Targets">The supported targets in display order. Cannot be <see langword="null" />.</param>
/// <param name="SuggestedTarget">
///     The conservative suggested target, or <see langword="null" /> when no suggestion is available.
/// </param>
/// <param name="TargetSetToken">The deterministic target-set token. Cannot be <see langword="null" />.</param>
/// <param name="UnsupportedTargetCount">The number of discovered targets that are not supported.</param>
public sealed record ComparisonTargetSet(
    IReadOnlyList<ComparisonTargetDescriptor> Targets,
    ComparisonTargetDescriptor? SuggestedTarget,
    string TargetSetToken,
    int UnsupportedTargetCount)
{
    /// <summary>
    ///     Gets the complete filtered target set before a continuation cursor is applied.
    /// </summary>
    /// <remarks>
    ///     Engine page shaping uses this immutable set to keep transport-size exclusions consistent across
    ///     continuation pages. For an unpaged or directly constructed set, the value defaults to
    ///     <see cref="Targets" />.
    /// </remarks>
    public IReadOnlyList<ComparisonTargetDescriptor> UnpagedTargets { get; init; } = Targets;
}
