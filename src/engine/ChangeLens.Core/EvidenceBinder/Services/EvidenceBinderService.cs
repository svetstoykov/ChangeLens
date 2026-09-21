using System.Security.Cryptography;
using System.Text;
using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.ChangeAnatomy.Helpers;
using ChangeLens.Core.ContextPolicy.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Interfaces;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceGraph.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Snapshots.Models;
using Microsoft.Extensions.Logging;
using EvidenceBinderModel = ChangeLens.Core.EvidenceBinder.Models.EvidenceBinder;

namespace ChangeLens.Core.EvidenceBinder.Services;

/// <summary>
///     Assembles a bounded, policy-filtered evidence binder from frozen analysis inputs.
/// </summary>
public sealed class EvidenceBinderService : IEvidenceBinderService
{
    private const double CharactersPerToken = 3.25;
    private const string DeveloperContextLabel = "developer-supplied hint, not evidence";

    private readonly EvidenceBinderOptions _options;
    private readonly ILogger<EvidenceBinderService> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="EvidenceBinderService" /> class.
    /// </summary>
    /// <param name="options">The binder limits. Cannot be <see langword="null" />.</param>
    /// <param name="logger">The logger for binder outcomes and ladder flow. Cannot be <see langword="null" />.</param>
    public EvidenceBinderService(EvidenceBinderOptions options, ILogger<EvidenceBinderService> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        this._options = options;
        this._logger = logger;
    }

    /// <inheritdoc />
    public Result<EvidenceBinderModel> Assemble(EvidenceBinderRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (!this.ValidateOptions(out var optionsError))
        {
            this._logger.LogWarning(
                "Evidence binder rejected the configured limits for run {RunId} with {ErrorCode}.",
                request.Run.RunId, EvidenceBinderErrorCode.InvalidOptions);
            return OperationError.Validation(optionsError!, EvidenceBinderErrorCode.InvalidOptions);
        }

        var maxCharacters = this._options.EffectiveMaximumBinderCharacters;
        var targetCharacters = this._options.TargetBinderCharacters;
        var comparison = new BinderComparison(
            request.Run.RunId,
            request.Run.Repository.CanonicalRepositoryPathKey,
            request.Run.Comparison.Target,
            request.Snapshot.Manifest.TargetRevision,
            request.Snapshot.Manifest.HeadRevision,
            request.Snapshot.Manifest.MergeBaseRevision,
            request.Run.CapturedAtUnixMilliseconds ?? 0,
            request.Snapshot.ExcludedUncommittedCounts);

        var omissions = new List<(string Kind, string Reason, string Item)>();
        var developerContext = BuildDeveloperContext(request.DeveloperContext, omissions);
        var invariantNodeIds = new HashSet<string>(StringComparer.Ordinal);
        var decisionsById = request.Policy.Decisions
            .GroupBy(decision => decision.NodeId, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal);

        foreach (var duplicateId in request.Policy.Decisions
                     .GroupBy(decision => decision.NodeId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            invariantNodeIds.Add(duplicateId);
        }

        foreach (var duplicateId in request.Policy.DisclosedNodes
                     .GroupBy(node => node.NodeId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            invariantNodeIds.Add(duplicateId);
        }

        var evidence = new List<BinderEvidence>();
        var salienceById = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var node in request.Policy.DisclosedNodes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!decisionsById.TryGetValue(node.NodeId, out var decision) || invariantNodeIds.Contains(node.NodeId)
                || !MatchesDecision(node, decision))
            {
                invariantNodeIds.Add(node.NodeId);
                omissions.Add((EvidenceBinderOmissionKind.PolicyInvariantViolation, "policyInvariantViolated", node.NodeId));
                continue;
            }

            salienceById[node.NodeId] = node.Salience;
            evidence.Add(new BinderEvidence(
                node.NodeId,
                node.Side,
                node.Path,
                node.StartLine,
                node.EndLine,
                node.IsChangedFile,
                node.Origins,
                evidence.Count + 1,
                node.Text,
                node.ContentHash,
                node.DisclosedHash,
                node.IsRedacted,
                node.Truncated));
        }

        foreach (var decision in request.Policy.Decisions.Where(decision => decision.Verdict == ContextPolicyVerdict.Exclude))
        {
            omissions.Add((EvidenceBinderOmissionKind.PolicyExcluded, decision.ExclusionReason ?? "excluded", decision.NodeId));
        }

        foreach (var decision in request.Policy.EdgeDecisions.Where(decision => decision.MatchedValueWithheld))
        {
            omissions.Add((EvidenceBinderOmissionKind.MatchedValueWithheld, "matchedValueWithheldByPolicy", decision.EdgeId));
        }

        var withheldEdgeIds = request.Policy.EdgeDecisions
            .Where(decision => decision.MatchedValueWithheld)
            .Select(decision => decision.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        var admittedIds = evidence.Select(node => node.NodeId).ToHashSet(StringComparer.Ordinal);
        var edges = request.Policy.DisclosedEdges
            .Where(edge => admittedIds.Contains(edge.FromNodeId) && admittedIds.Contains(edge.ToNodeId))
            .Select(edge => new BinderMatchEdge(
                edge.EdgeId,
                edge.FromNodeId,
                edge.ToNodeId,
                edge.Kind,
                withheldEdgeIds.Contains(edge.EdgeId) ? null : edge.MatchedValue,
                edge.FromAnchor,
                edge.ToAnchor,
                edge.Contribution))
            .ToList();

        var changedFiles = request.Snapshot.Manifest.Entries
            .Select(entry => new BinderChangedFile(
                entry.Path,
                entry.OriginalPath,
                entry.Category,
                entry.MergeBaseEntryMode,
                entry.HeadEntryMode,
                evidence
                    .Where(node => node.Path == entry.Path || (entry.OriginalPath is not null && node.Path == entry.OriginalPath))
                    .Select(node => node.NodeId)
                    .ToList()))
            .ToList();

        var excludedPaths = request.Policy.Decisions
            .Where(decision => decision.Verdict == ContextPolicyVerdict.Exclude)
            .Select(decision => decision.Path)
            .ToHashSet(StringComparer.Ordinal);
        var orientation = BuildOrientation(
            request.AfterTree,
            changedFiles.Select(file => file.Path).Concat(evidence.Select(node => node.Path)),
            excludedPaths,
            this._options);
        var ladderSteps = new List<string>();

        IReadOnlyList<BinderOmission> CoalescedOmissions()
        {
            var all = omissions.Concat(changedFiles.Where(file => file.EvidenceNodeIds.Count == 0).Select(file =>
            {
                var skipped = request.Anatomy.Files.FirstOrDefault(fileAnatomy => fileAnatomy.Path == file.Path)?.SkipReason;
                return skipped is null
                    ? (EvidenceBinderOmissionKind.NoEvidenceSelected, "noDisclosedNodeForChangedFile", file.Path)
                    : (EvidenceBinderOmissionKind.FileNotRead, skipped, file.Path);
            }));
            return all
                .GroupBy(entry => (entry.Item1, entry.Item2))
                .OrderBy(group => group.Key.Item1, StringComparer.Ordinal)
                .ThenBy(group => group.Key.Item2, StringComparer.Ordinal)
                .Select(group => new BinderOmission(
                    group.Key.Item1,
                    group.Count(),
                    group.Key.Item2,
                    group.Take(this._options.MaximumOmissionItems).Select(entry => CapItem(entry.Item3)).ToList()))
                .ToList();
        }

        BinderContract Contract() => new(
            CuratorContractConstants.RelationshipKinds,
            CuratorContractConstants.TrackShapes,
            new CuratorLimits(
                this._options.MaximumTracks,
                this._options.MaximumParticipantsPerTrack,
                this._options.MaximumRelationshipsPerTrack,
                this._options.MaximumItemsPerTrack,
                this._options.MaximumStatementCharacters,
                CuratorContractConstants.IdFormat));

        EvidenceBinderModel BuildBinder()
        {
            var rankedEvidence = evidence
                .Select((node, index) => node with { Rank = index + 1 })
                .ToList();
            var diagnostics = new BinderDiagnostics(
                0,
                0,
                targetCharacters,
                maxCharacters,
                false,
                ladderSteps,
                rankedEvidence.Count,
                rankedEvidence.Count(node => node.IsRedacted),
                request.Policy.Decisions.Count(decision => decision.Verdict == ContextPolicyVerdict.Exclude),
                edges.Count,
                changedFiles.Count,
                changedFiles.Count(file => file.EvidenceNodeIds.Count == 0),
                orientation.Directories.Sum(directory => directory.Paths.Count),
                invariantNodeIds.OrderBy(nodeId => nodeId, StringComparer.Ordinal).ToList());
            return new EvidenceBinderModel(
                comparison,
                developerContext,
                changedFiles,
                rankedEvidence,
                edges,
                orientation,
                Contract(),
                CoalescedOmissions(),
                diagnostics);
        }

        int Measure() => EvidenceBinderJson.SerializePayload(BuildBinder()).Length;
        bool MeetsTarget() => Measure() <= targetCharacters;

        void DropNodes(string step, Func<BinderEvidence, bool> selector, bool protectLastPerChangedFile = false)
        {
            foreach (var nodeId in evidence
                         .Where(selector)
                         .OrderBy(node => salienceById.GetValueOrDefault(node.NodeId))
                         .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                         .Select(node => node.NodeId)
                         .ToList())
            {
                if (MeetsTarget())
                {
                    break;
                }

                if (protectLastPerChangedFile && changedFiles.Any(file => file.EvidenceNodeIds.Count == 1 && file.EvidenceNodeIds[0] == nodeId))
                {
                    continue;
                }

                var index = evidence.FindIndex(node => node.NodeId == nodeId);
                if (index < 0)
                {
                    continue;
                }

                evidence.RemoveAt(index);
                edges.RemoveAll(edge => edge.FromNodeId == nodeId || edge.ToNodeId == nodeId);
                for (var fileIndex = 0; fileIndex < changedFiles.Count; fileIndex++)
                {
                    var file = changedFiles[fileIndex];
                    if (!file.EvidenceNodeIds.Contains(nodeId, StringComparer.Ordinal))
                    {
                        continue;
                    }

                    var retainedIds = file.EvidenceNodeIds.Where(id => !string.Equals(id, nodeId, StringComparison.Ordinal)).ToList();
                    changedFiles[fileIndex] = file with { EvidenceNodeIds = retainedIds };
                }

                omissions.Add((EvidenceBinderOmissionKind.BudgetDropped, step, nodeId));
                if (!ladderSteps.Contains(step, StringComparer.Ordinal))
                {
                    ladderSteps.Add(step);
                }
            }
        }

        if (!MeetsTarget())
        {
            DropNodes(EvidenceBinderLadderStep.CandidateHeadNode, node => node.Origins.Contains(EvidenceNodeOrigin.CandidateHead));
            DropNodes(EvidenceBinderLadderStep.CandidateNodeSalience, node => !node.IsChangedFile);
            if (!MeetsTarget() && orientation.Directories.Count > 0)
            {
                foreach (var directory in orientation.Directories)
                {
                    omissions.Add((EvidenceBinderOmissionKind.BudgetDropped, EvidenceBinderLadderStep.Orientation, directory.Path));
                }

                orientation = new BinderOrientation([]);
                ladderSteps.Add(EvidenceBinderLadderStep.Orientation);
            }

            DropNodes(EvidenceBinderLadderStep.ChangedFileNodeSalience, node => node.IsChangedFile, true);
            DropNodes(EvidenceBinderLadderStep.ChangedFileNodeSalience, node => node.IsChangedFile);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var binder = BuildBinder();
        var characterCount = EvidenceBinderJson.SerializePayload(binder).Length;
        if (characterCount > maxCharacters)
        {
            this._logger.LogWarning(
                "Evidence binder for run {RunId} measured {BinderCharacterCount} characters after ladder steps {LadderSteps}, "
                + "exceeding the {BudgetCharacters}-character cap with {ErrorCode}.",
                request.Run.RunId, characterCount, ladderSteps, maxCharacters, EvidenceBinderErrorCode.BudgetExceeded);
            return OperationError.UnprocessableInput(
                $"The curator payload is {characterCount} characters after the evidence budget ladder, exceeding the "
                + $"{maxCharacters}-character hard cap.", EvidenceBinderErrorCode.BudgetExceeded);
        }

        var diagnostics = binder.Diagnostics with
        {
            BinderCharacterCount = characterCount,
            TokenEstimate = (int)Math.Ceiling(characterCount / CharactersPerToken),
            BudgetBinding = ladderSteps.Count > 0,
        };

        this._logger.LogInformation(
            "Evidence binder assembled for run {RunId} with {EvidenceCount} evidence nodes, {MatchEdgeCount} match edges, and "
            + "{BinderCharacterCount} characters against a {TargetCharacters}-character target, budget binding {BudgetBinding}.",
            request.Run.RunId, diagnostics.EvidenceCount, diagnostics.MatchEdgeCount, characterCount, targetCharacters,
            diagnostics.BudgetBinding);
        if (ladderSteps.Count > 0)
        {
            var droppedNodeCount = omissions.Count(entry =>
                entry.Kind == EvidenceBinderOmissionKind.BudgetDropped && entry.Reason != EvidenceBinderLadderStep.Orientation);
            this._logger.LogDebug(
                "Evidence binder ladder for run {RunId} applied {LadderSteps}, dropping {BudgetDroppedNodeCount} evidence nodes and "
                + "leaving {OrientationPathCount} orientation paths across {ChangedFileCount} changed files, "
                + "{ChangedFilesWithoutEvidenceCount} of them without evidence.",
                request.Run.RunId, ladderSteps, droppedNodeCount, diagnostics.OrientationPathCount, diagnostics.ChangedFileCount,
                diagnostics.ChangedFilesWithoutEvidenceCount);
        }

        return binder with { Diagnostics = diagnostics };
    }

    private BinderHint? BuildDeveloperContext(string? context, List<(string Kind, string Reason, string Item)> omissions)
    {
        if (string.IsNullOrWhiteSpace(context))
        {
            return null;
        }

        var truncated = context.Length > this._options.MaximumDeveloperContextCharacters;
        if (truncated)
        {
            omissions.Add((EvidenceBinderOmissionKind.DeveloperContextTruncated, "exceededCharacterCap", "request"));
        }

        return new BinderHint(
            truncated ? context[..this._options.MaximumDeveloperContextCharacters] : context,
            DeveloperContextLabel,
            truncated);
    }

    private bool ValidateOptions(out string? error)
    {
        if (this._options.ContextWindowTokens <= 0 || this._options.CuratorOutputCharacters < 0 || this._options.PromptReserveCharacters < 0
            || this._options.MaximumBinderCharacters is <= 0 || this._options.EffectiveMaximumBinderCharacters <= 0
            || this._options.TargetUtilization is <= 0 or > 1 || this._options.MaximumOrientationPathsPerDirectory <= 0
            || this._options.MaximumOrientationPaths <= 0 || this._options.MaximumDeveloperContextCharacters <= 0
            || this._options.MaximumOmissionItems <= 0 || this._options.MaximumOmissionItemCharacters <= 0
            || this._options.MaximumTracks <= 0 || this._options.MaximumParticipantsPerTrack <= 0
            || this._options.MaximumRelationshipsPerTrack <= 0 || this._options.MaximumItemsPerTrack <= 0
            || this._options.MaximumStatementCharacters <= 0)
        {
            error = "Every evidence binder limit must be positive, with non-negative reserves.";
            return false;
        }

        error = null;
        return true;
    }

    private string CapItem(string value)
    {
        if (value.Length <= this._options.MaximumOmissionItemCharacters)
        {
            return value;
        }

        var suffix = "…" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..8];
        if (this._options.MaximumOmissionItemCharacters <= suffix.Length)
        {
            return suffix[..this._options.MaximumOmissionItemCharacters];
        }

        return value[..(this._options.MaximumOmissionItemCharacters - suffix.Length)] + suffix;
    }

    private static bool MatchesDecision(DisclosedEvidenceNode node, ContextPolicyDecision decision) => decision.Verdict switch
    {
        ContextPolicyVerdict.Allow => decision.Path == node.Path && !node.IsRedacted && node.DisclosedHash is null,
        ContextPolicyVerdict.Redact => decision.Path == node.Path && node.IsRedacted && node.DisclosedHash is not null
            && node.DisclosedHash == Hash(node.Text),
        _ => false,
    };

    private static string Hash(string text) => "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static BinderOrientation BuildOrientation(
        FrozenGitTreeListing tree,
        IEnumerable<string> focusPaths,
        IReadOnlySet<string> excludedPaths,
        EvidenceBinderOptions options)
    {
        var byDirectory = tree.Files
            .GroupBy(file => DirectoryOf(file.Path), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(file => file.Path)
                    .Where(path => !excludedPaths.Contains(path) && ChangeAnatomyPathRules.ExclusionReason(path) is null)
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList(),
                StringComparer.Ordinal);
        var directories = new List<BinderDirectory>();
        var remaining = options.MaximumOrientationPaths;
        foreach (var directory in focusPaths.Select(DirectoryOf).Distinct(StringComparer.Ordinal).OrderBy(path => path, StringComparer.Ordinal))
        {
            if (remaining <= 0)
            {
                break;
            }

            if (!byDirectory.TryGetValue(directory, out var siblings))
            {
                continue;
            }

            var take = Math.Min(Math.Min(options.MaximumOrientationPathsPerDirectory, remaining), siblings.Count);
            directories.Add(new BinderDirectory(directory, siblings.Take(take).ToList(), siblings.Count - take));
            remaining -= take;
        }

        return new BinderOrientation(directories);
    }

    private static string DirectoryOf(string path)
    {
        var index = path.LastIndexOf('/');
        return index < 0 ? string.Empty : path[..index];
    }
}
