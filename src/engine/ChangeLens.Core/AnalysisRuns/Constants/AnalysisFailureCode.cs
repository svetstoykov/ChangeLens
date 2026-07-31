namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides stable terminal failure codes for the analysis pipeline.
/// </summary>
public static class AnalysisFailureCode
{
    /// <summary>An unexpected exception crossed the pipeline boundary.</summary>
    public const string UnexpectedFailure = "analysis.unexpectedFailure";
}
