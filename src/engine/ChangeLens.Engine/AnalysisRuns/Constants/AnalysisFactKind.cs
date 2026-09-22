namespace ChangeLens.Engine.AnalysisRuns.Constants;

/// <summary>Provides stable protocol identifiers for discovered analysis facts.</summary>
internal static class AnalysisFactKind
{
    internal const string ChangedFilesCaptured = "changedFilesCaptured";
    internal const string ExcludedUncommittedFiles = "excludedUncommittedFiles";
    internal const string CorrespondenceCandidates = "correspondenceCandidates";
    internal const string DisclosedEvidenceNodes = "disclosedEvidenceNodes";
    internal const string ValidationRemovals = "validationRemovals";
}
