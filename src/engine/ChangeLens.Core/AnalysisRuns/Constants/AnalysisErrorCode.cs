namespace ChangeLens.Core.AnalysisRuns.Constants;

/// <summary>
///     Provides stable Core-originated analysis error codes.
/// </summary>
public static class AnalysisErrorCode
{
    /// <summary>Identifies a repository path that cannot be resolved to an available repository.</summary>
    public const string RepositoryUnavailable = "analysis.repositoryUnavailable";

    /// <summary>Identifies a run identifier with no matching durable run.</summary>
    public const string UnknownRun = "analysis.unknownRun";
}
