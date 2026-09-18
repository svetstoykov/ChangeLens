namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents the curator-facing binder payload without engine-only match edges.
/// </summary>
/// <param name="Comparison">The comparison identity and capture metadata.</param>
/// <param name="DeveloperContext">The bounded developer hint, or <see langword="null" />.</param>
/// <param name="ChangedFiles">Every captured changed-file entry.</param>
/// <param name="Evidence">The policy-disclosed evidence nodes.</param>
/// <param name="Orientation">The bounded sibling-path orientation.</param>
/// <param name="Contract">The closed curator contract.</param>
/// <param name="Omissions">The grouped omissions and their reasons.</param>
internal sealed record BinderPayload(
    BinderComparison Comparison,
    BinderHint? DeveloperContext,
    IReadOnlyList<BinderChangedFile> ChangedFiles,
    IReadOnlyList<BinderEvidence> Evidence,
    BinderOrientation Orientation,
    BinderContract Contract,
    IReadOnlyList<BinderOmission> Omissions);
