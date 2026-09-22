namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides product-owned bounds for analysis run processing and protocol payloads.
/// </summary>
public static class AnalysisRunLimits
{
    /// <summary>The maximum number of runs the processor takes concurrently.</summary>
    public const int MaximumConcurrentRuns = 1;

    /// <summary>The maximum complete UTF-8 poll-response size with the production serializer.</summary>
    /// <remarks>Applies to every poll whose reading model is absent.</remarks>
    public const int PollSummaryMaxBytes = 48 * 1024;

    /// <summary>
    ///     The maximum complete UTF-8 response-line size the desktop shell accepts, which bounds a completed poll that
    ///     carries its reading model.
    /// </summary>
    public const int MaximumPollResponseBytes = 2 * 1024 * 1024;
}
