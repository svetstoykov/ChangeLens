using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.DraftValidation.Models;
using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.AnalysisRuns.Constants;
using ChangeLens.Engine.AnalysisRuns.Models;

namespace ChangeLens.Engine.AnalysisRuns.Helpers;

/// <summary>
///     Maps a published reading model and its validation removals to their engine protocol representation.
/// </summary>
/// <remarks>
///     <see cref="ReadingModel.Comparison" /> is not mapped: the run summary already carries the repository, target,
///     revisions, and excluded-uncommitted counts. Any value outside a closed protocol vocabulary fails the whole
///     mapping with <c>analysis.unmappedReadingModel</c> rather than emitting a value the schema would refuse.
/// </remarks>
internal static class ReadingModelProtocolMapper
{
    /// <summary>Maps one published reading model to its protocol representation.</summary>
    /// <param name="model">The published reading model. Cannot be <see langword="null" />.</param>
    /// <returns>The protocol reading model, or an error when a value has no approved protocol representation.</returns>
    internal static Result<ReadingModelResult> ToProtocol(ReadingModel model)
    {
        if (!IsMappable(model))
        {
            return OperationError.InternalError(
                "The reading model holds a value that is not approved for the engine protocol.", AnalysisProtocolErrorCode.UnmappedReadingModel);
        }

        return new ReadingModelResult(
            model.Thesis is null ? null : ToStatement(model.Thesis),
            [.. model.Areas.Select(ToArea)],
            [.. model.Citations.Select(ToCitation)],
            [.. model.Evidence.Select(ToEvidence)],
            [.. model.Limitations.Select(limitation =>
                new ReadingLimitationResult(ToLimitationKind(limitation.Kind)!, limitation.Path, limitation.Detail))],
            [.. model.OmissionSummaries.Select(summary => new ReadingOmissionSummaryResult(
                summary.SourceKind, summary.Reason, summary.TotalCount, summary.SampleCount, summary.ResolvedSampleCount))],
            [.. model.Assurances.Select(assurance =>
                new ReadingAssuranceResult(ToAssuranceKind(assurance.Kind)!, assurance.Detail, assurance.ClaimId))]);
    }

    /// <summary>Maps validation removals to their protocol representation, preserving order.</summary>
    /// <param name="removals">The removals. Cannot be <see langword="null" />.</param>
    /// <returns>The protocol removals, or an error when a scope has no approved protocol representation.</returns>
    internal static Result<IReadOnlyList<ValidationRemovalResult>> ToProtocol(IReadOnlyList<ValidationRemoval> removals)
    {
        var mapped = new List<ValidationRemovalResult>(removals.Count);
        foreach (var removal in removals)
        {
            if (!ReadingModelProtocolConstants.RemovalScopes.Contains(removal.Scope))
            {
                return OperationError.InternalError(
                    "The validation removal scope is not approved for the engine protocol.", AnalysisProtocolErrorCode.UnmappedReadingModel);
            }

            mapped.Add(new ValidationRemovalResult(removal.Scope, removal.Id, removal.Reason));
        }

        return mapped;
    }

    private static ReadingAreaResult ToArea(ReadingArea area) => new(
        area.Id,
        area.Title,
        area.Summary is null ? null : ToStatement(area.Summary),
        ToShape(area.Shape)!,
        [.. area.Participants.Select(participant => new ReadingParticipantResult(
            participant.Id, participant.Name, participant.Role, participant.Changed, participant.EvidenceNodeIds))],
        [.. area.Relationships.Select(relationship => new ReadingRelationshipResult(
            relationship.ClaimId, relationship.Id, relationship.FromParticipantId, relationship.ToParticipantId, relationship.Kind,
            relationship.Explanation, ToTrust(relationship.Trust)!, relationship.EvidenceNodeIds))],
        [.. area.OrderedSteps.Select(ToStatement)],
        [.. area.Purposes.Select(ToStatement)]);

    private static ReadingStatementResult ToStatement(ReadingStatement statement) =>
        new(statement.ClaimId, statement.Text, ToTrust(statement.Trust)!, statement.EvidenceNodeIds);

    private static ReadingCitationResult ToCitation(Citation citation) => new(
        citation.ClaimId,
        citation.NodeId,
        ToSide(citation.Side)!,
        citation.Path,
        citation.ObjectId,
        citation.StartLine,
        citation.EndLine,
        [.. citation.Focus.Select(range => new ReadingFocusRangeResult(range.NodeId, range.StartLine, range.EndLine))],
        ToProvenance(citation.Provenance)!);

    private static ReadingEvidenceResult ToEvidence(ReadingEvidence evidence) => new(
        evidence.NodeId,
        evidence.Path,
        ToSide(evidence.Side)!,
        evidence.StartLine,
        evidence.EndLine,
        evidence.IsChangedFile,
        evidence.IsRedacted,
        evidence.IsTruncated,
        evidence.Text);

    private static bool IsMappable(ReadingModel model)
    {
        var statements = model.Areas
            .SelectMany(area => area.OrderedSteps.Concat(area.Purposes).Append(area.Summary))
            .Append(model.Thesis)
            .OfType<ReadingStatement>();
        var relationships = model.Areas.SelectMany(area => area.Relationships);

        return model.Areas.All(area => ToShape(area.Shape) is not null)
            && statements.All(statement => ToTrust(statement.Trust) is not null)
            && relationships.All(relationship => ToTrust(relationship.Trust) is not null)
            && model.Citations.All(citation => ToSide(citation.Side) is not null && ToProvenance(citation.Provenance) is not null)
            && model.Evidence.All(evidence => ToSide(evidence.Side) is not null)
            && model.Limitations.All(limitation => ToLimitationKind(limitation.Kind) is not null)
            && model.OmissionSummaries.All(summary => ReadingModelProtocolConstants.OmissionSourceKinds.Contains(summary.SourceKind))
            && model.Assurances.All(assurance => ToAssuranceKind(assurance.Kind) is not null);
    }

    private static string? ToShape(ReadingShape shape) => shape switch
    {
        ReadingShape.Walk => ReadingModelProtocolConstants.ShapeWalk,
        ReadingShape.ParticipantMap => ReadingModelProtocolConstants.ShapeParticipantMap,
        ReadingShape.PurposeCards => ReadingModelProtocolConstants.ShapePurposeCards,
        ReadingShape.ParticipantList => ReadingModelProtocolConstants.ShapeParticipantList,
        _ => null,
    };

    private static string? ToTrust(ReadingTrust trust) => trust switch
    {
        ReadingTrust.Unchecked => ReadingModelProtocolConstants.TrustUnchecked,
        ReadingTrust.Derived => ReadingModelProtocolConstants.TrustDerived,
        ReadingTrust.Checked => ReadingModelProtocolConstants.TrustChecked,
        _ => null,
    };

    private static string? ToProvenance(CitationProvenance provenance) => provenance switch
    {
        CitationProvenance.Unchecked => ReadingModelProtocolConstants.TrustUnchecked,
        CitationProvenance.Derived => ReadingModelProtocolConstants.TrustDerived,
        CitationProvenance.Checked => ReadingModelProtocolConstants.TrustChecked,
        _ => null,
    };

    private static string? ToSide(ChangeAnatomySide side) => side switch
    {
        ChangeAnatomySide.Before => ReadingModelProtocolConstants.SideBefore,
        ChangeAnatomySide.After => ReadingModelProtocolConstants.SideAfter,
        _ => null,
    };

    private static string? ToLimitationKind(ReadingLimitationKind kind) => kind switch
    {
        ReadingLimitationKind.FileNotRead => ReadingModelProtocolConstants.LimitationFileNotRead,
        ReadingLimitationKind.FileNotQuoted => ReadingModelProtocolConstants.LimitationFileNotQuoted,
        ReadingLimitationKind.UncommittedWorkExcluded => ReadingModelProtocolConstants.LimitationUncommittedWorkExcluded,
        _ => null,
    };

    private static string? ToAssuranceKind(ReadingAssuranceKind kind) => kind switch
    {
        ReadingAssuranceKind.CheckerNotRun => ReadingModelProtocolConstants.AssuranceCheckerNotRun,
        ReadingAssuranceKind.CheckerFailed => ReadingModelProtocolConstants.AssuranceCheckerFailed,
        ReadingAssuranceKind.TestsNotExecuted => ReadingModelProtocolConstants.AssuranceTestsNotExecuted,
        ReadingAssuranceKind.BuildNotExecuted => ReadingModelProtocolConstants.AssuranceBuildNotExecuted,
        ReadingAssuranceKind.RepositoryNotFullyRead => ReadingModelProtocolConstants.AssuranceRepositoryNotFullyRead,
        ReadingAssuranceKind.ClaimNotCited => ReadingModelProtocolConstants.AssuranceClaimNotCited,
        ReadingAssuranceKind.DuplicateClaimId => ReadingModelProtocolConstants.AssuranceDuplicateClaimId,
        _ => null,
    };
}
