using ChangeLens.Core.AnalysisRuns.Models;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.Correspondence.Models;
using ChangeLens.Core.Snapshots.Models;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;
using EvidenceGraphModel = ChangeLens.Core.EvidenceGraph.Models.EvidenceGraph;

namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Holds the in-memory products one analysis run passes from each pipeline step to the next.
/// </summary>
/// <remarks>
///     None of these products is durable. A run that stops before its terminal commit leaves them behind, and startup
///     recovery interrupts that run rather than resuming it.
/// </remarks>
internal sealed class AnalysisPipelineRun
{
    /// <summary>Gets or sets the run detail read before capture.</summary>
    internal AnalysisRunDetail? Detail { get; set; }

    /// <summary>Gets or sets the committed capture.</summary>
    internal SnapshotCapture? Capture { get; set; }

    /// <summary>Gets or sets the correspondence ranking discover produced.</summary>
    internal CorrespondenceRanking? Ranking { get; set; }

    /// <summary>Gets or sets the evidence graph discover produced.</summary>
    internal EvidenceGraphModel? Graph { get; set; }

    /// <summary>Gets or sets the context policy outcome discover produced.</summary>
    internal ContextPolicyOutcome? Policy { get; set; }

    /// <summary>Gets or sets the evidence binder discover assembled.</summary>
    internal EvidenceBinderModel? Binder { get; set; }

    /// <summary>Gets or sets the renderable projection collect produced for the terminal commit.</summary>
    internal AnalysisReadingProjection? Projection { get; set; }
}
