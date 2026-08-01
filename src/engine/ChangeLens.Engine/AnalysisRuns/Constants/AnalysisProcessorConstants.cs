namespace ChangeLens.Engine.AnalysisRuns.Constants;

/// <summary>
///     Provides capability-owned timing constants for the analysis processor loop.
/// </summary>
internal static class AnalysisProcessorConstants
{
    /// <summary>The interval between database fallback checks when no wake-up signal arrives.</summary>
    internal static readonly TimeSpan DatabaseFallbackInterval = TimeSpan.FromSeconds(1);
}
