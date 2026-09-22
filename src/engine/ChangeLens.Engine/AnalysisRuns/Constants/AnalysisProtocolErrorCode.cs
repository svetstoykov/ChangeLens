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

    /// <summary>Identifies a reading-model or removal value that has no approved protocol representation.</summary>
    internal const string UnmappedReadingModel = "analysis.unmappedReadingModel";

    /// <summary>Identifies a stored reading projection of a completed run that is not readable protocol JSON.</summary>
    internal const string UnreadableReadingModel = "analysis.unreadableReadingModel";
}
