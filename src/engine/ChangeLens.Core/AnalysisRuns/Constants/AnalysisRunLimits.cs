namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides product-owned bounds for analysis run processing and protocol payloads.
/// </summary>
public static class AnalysisRunLimits
{
    /// <summary>The maximum number of runs the processor takes concurrently.</summary>
    public const int MaximumConcurrentRuns = 1;

    /// <summary>The maximum complete UTF-8 poll-response size with the production serializer.</summary>
    public const int PollProjectionMaxBytes = 48 * 1024;
}
