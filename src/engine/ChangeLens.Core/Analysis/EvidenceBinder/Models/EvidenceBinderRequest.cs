using ChangeLens.Core.Analysis.ContextPolicy.Models;
using ChangeLens.Core.Analysis.Snapshots.Models;
using ChangeAnatomyModel = ChangeLens.Core.Analysis.ChangeAnatomy.Models.ChangeAnatomy;

namespace ChangeLens.Core.Analysis.EvidenceBinder.Models;

/// <summary>
///     Represents the frozen inputs required to assemble one evidence binder.
/// </summary>
/// <param name="Run">The accepted analysis run identity and capture timestamp. Cannot be <see langword="null" />.</param>
/// <param name="Snapshot">The committed frozen snapshot. Cannot be <see langword="null" />.</param>
/// <param name="Policy">The context-policy outcome. Cannot be <see langword="null" />.</param>
/// <param name="AfterTree">The bounded captured HEAD tree. Cannot be <see langword="null" />.</param>
/// <param name="Anatomy">The deterministic change anatomy. Cannot be <see langword="null" />.</param>
/// <param name="DeveloperContext">The optional developer hint, or <see langword="null" />.</param>
public sealed record EvidenceBinderRequest(
    EvidenceBinderRunContext Run,
    SnapshotCapture Snapshot,
    ContextPolicyOutcome Policy,
    FrozenGitTreeListing AfterTree,
    ChangeAnatomyModel Anatomy,
    string? DeveloperContext);
