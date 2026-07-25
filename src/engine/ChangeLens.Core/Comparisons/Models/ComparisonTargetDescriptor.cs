namespace ChangeLens.Core.Comparisons.Models;

/// <summary>
///     Represents an immutable comparison-target identity and resolved revision.
/// </summary>
/// <param name="Kind">The supported target kind.</param>
/// <param name="Name">The target display name. Cannot be <see langword="null" />.</param>
/// <param name="FullName">The full Git reference name. Cannot be <see langword="null" />.</param>
/// <param name="Revision">The resolved full object identifier. Cannot be <see langword="null" />.</param>
public sealed record ComparisonTargetDescriptor(
    ComparisonTargetKind Kind,
    string Name,
    string FullName,
    string Revision);
