using ChangeLens.Core.Analysis.Snapshots.Models;
using ChangeLens.Core.AnalysisRuns.Models;

namespace ChangeLens.Engine.Analysis.Pipeline.Models;

/// <summary>
///     Represents the immutable handoff produced when the capture step completes.
/// </summary>
/// <param name="Detail">The run detail read before capture. Cannot be <see langword="null" />.</param>
/// <param name="Capture">The committed snapshot capture. Cannot be <see langword="null" />.</param>
internal sealed record CaptureStepOutput(AnalysisRunDetail Detail, SnapshotCapture Capture);
