namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides stable terminal failure codes for the analysis pipeline.
/// </summary>
public static class AnalysisFailureCode
{
    /// <summary>An unexpected exception crossed the pipeline boundary.</summary>
    public const string UnexpectedFailure = "analysis.unexpectedFailure";

    /// <summary>The resolved target or HEAD revision moved between acceptance and the capture cut.</summary>
    public const string StaleAtCapture = "analysis.staleAtCapture";

    /// <summary>Capture could not produce a truthful manifest from bounded Git inspection.</summary>
    public const string CaptureFailed = "analysis.captureFailed";
}
