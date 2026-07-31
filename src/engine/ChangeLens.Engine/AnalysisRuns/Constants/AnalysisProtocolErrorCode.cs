namespace ChangeLens.Engine.AnalysisRuns.Constants;

/// <summary>
///     Provides stable Engine-originated analysis error codes.
/// </summary>
internal static class AnalysisProtocolErrorCode
{
    /// <summary>Identifies a durable run state that has no approved protocol representation.</summary>
    internal const string UnmappedRunState = "analysis.unmappedRunState";

    /// <summary>Identifies a terminal kind that has no approved protocol representation.</summary>
    internal const string UnmappedTerminalKind = "analysis.unmappedTerminalKind";
}
