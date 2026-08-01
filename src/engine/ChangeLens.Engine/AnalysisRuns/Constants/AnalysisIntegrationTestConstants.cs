namespace ChangeLens.Engine.AnalysisRuns.Constants;

/// <summary>
///     Provides analysis process-control settings available to Debug integration-test hosts.
/// </summary>
internal static class AnalysisIntegrationTestConstants
{
    /// <summary>The environment variable containing the file that releases gated analysis pipeline work.</summary>
    internal const string PipelineReleaseFileEnvironmentVariable =
        "ChangeLens__IntegrationTesting__AnalysisPipelineReleaseFile";

    /// <summary>The configuration key containing the file that releases gated analysis pipeline work.</summary>
    internal const string PipelineReleaseFileConfigurationKey =
        "ChangeLens:IntegrationTesting:AnalysisPipelineReleaseFile";

    /// <summary>The environment variable containing the file that records entry into the gated pipeline.</summary>
    internal const string PipelineEnteredFileEnvironmentVariable =
        "ChangeLens__IntegrationTesting__AnalysisPipelineEnteredFile";

    /// <summary>The configuration key containing the file that records entry into the gated pipeline.</summary>
    internal const string PipelineEnteredFileConfigurationKey =
        "ChangeLens:IntegrationTesting:AnalysisPipelineEnteredFile";

    /// <summary>The environment variable containing the file that records the start of pipeline step execution.</summary>
    internal const string PipelineStepsStartedFileEnvironmentVariable =
        "ChangeLens__IntegrationTesting__AnalysisPipelineStepsStartedFile";

    /// <summary>The configuration key containing the file that records the start of pipeline step execution.</summary>
    internal const string PipelineStepsStartedFileConfigurationKey =
        "ChangeLens:IntegrationTesting:AnalysisPipelineStepsStartedFile";

    /// <summary>The interval between checks for the pipeline release file.</summary>
    internal static readonly TimeSpan PipelineReleasePollInterval = TimeSpan.FromMilliseconds(10);
}
