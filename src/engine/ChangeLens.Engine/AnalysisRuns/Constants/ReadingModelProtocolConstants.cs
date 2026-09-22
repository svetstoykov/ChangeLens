namespace ChangeLens.Engine.AnalysisRuns.Constants;

/// <summary>Provides the closed protocol vocabularies of the published reading model and validation removals.</summary>
internal static class ReadingModelProtocolConstants
{
    internal const string ShapeWalk = "walk";
    internal const string ShapeParticipantMap = "participantMap";
    internal const string ShapePurposeCards = "purposeCards";
    internal const string ShapeParticipantList = "participantList";

    internal const string TrustUnchecked = "unchecked";
    internal const string TrustDerived = "derived";
    internal const string TrustChecked = "checked";

    internal const string SideBefore = "before";
    internal const string SideAfter = "after";

    internal const string LimitationFileNotRead = "fileNotRead";
    internal const string LimitationFileNotQuoted = "fileNotQuoted";
    internal const string LimitationUncommittedWorkExcluded = "uncommittedWorkExcluded";

    internal const string AssuranceCheckerNotRun = "checkerNotRun";
    internal const string AssuranceCheckerFailed = "checkerFailed";
    internal const string AssuranceTestsNotExecuted = "testsNotExecuted";
    internal const string AssuranceBuildNotExecuted = "buildNotExecuted";
    internal const string AssuranceRepositoryNotFullyRead = "repositoryNotFullyRead";
    internal const string AssuranceClaimNotCited = "claimNotCited";
    internal const string AssuranceDuplicateClaimId = "duplicateClaimId";

    internal const string OmissionFileNotRead = "fileNotRead";
    internal const string OmissionNoEvidenceSelected = "noEvidenceSelected";
    internal const string OmissionPolicyExcluded = "policyExcluded";

    internal const string RemovalScopeTrack = "track";
    internal const string RemovalScopeParticipant = "participant";
    internal const string RemovalScopeRelationship = "relationship";
    internal const string RemovalScopeStatement = "statement";

    /// <summary>The omission source kinds the protocol accepts.</summary>
    internal static readonly IReadOnlySet<string> OmissionSourceKinds =
        new HashSet<string>([OmissionFileNotRead, OmissionNoEvidenceSelected, OmissionPolicyExcluded], StringComparer.Ordinal);

    /// <summary>The validation removal scopes the protocol accepts.</summary>
    internal static readonly IReadOnlySet<string> RemovalScopes = new HashSet<string>(
        [RemovalScopeTrack, RemovalScopeParticipant, RemovalScopeRelationship, RemovalScopeStatement], StringComparer.Ordinal);
}
