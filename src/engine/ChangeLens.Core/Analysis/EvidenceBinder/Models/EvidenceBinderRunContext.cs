namespace ChangeLens.Core.Analysis.EvidenceBinder.Models;

/// <summary>
///     Represents the accepted run identity and capture timestamp needed to assemble an evidence binder.
/// </summary>
/// <param name="RunId">The accepted analysis run identifier.</param>
/// <param name="ComparisonTarget">The selected comparison target ref. Cannot be <see langword="null" />.</param>
/// <param name="CapturedAtUnixMilliseconds">The snapshot capture timestamp in Unix milliseconds.</param>
public sealed record EvidenceBinderRunContext(
    Guid RunId,
    string ComparisonTarget,
    long CapturedAtUnixMilliseconds);
