using ChangeLens.Core.ClaimChecking.Interfaces;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.EvidenceFrontier.Interfaces;
using ChangeLens.Core.MentalModels.Helpers;
using ChangeLens.Core.MentalModels.Models;
using ChangeLens.Core.Publication.Helpers;
using ChangeLens.Core.Publication.Interfaces;
using ChangeLens.Core.Publication.Models;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Core.Publication.Services;

/// <summary>Orchestrates optional checking, shape repair, citation construction, and frontier publication.</summary>
public sealed class PublicationService : IPublicationService
{
    private readonly IClaimCheckingService _claimCheckingService;
    private readonly IEvidenceFrontierService _evidenceFrontierService;
    private readonly ClaimCheckingOptions _claimCheckingOptions;
    private readonly ILogger<PublicationService> _logger;
    private readonly IClaimChecker? _claimChecker;

    /// <summary>Initializes a publication orchestrator.</summary>
    /// <param name="claimCheckingService">The deterministic claim-checking service.</param>
    /// <param name="evidenceFrontierService">The evidence-frontier service.</param>
    /// <param name="claimCheckingOptions">The checker options.</param>
    /// <param name="logger">The publication logger.</param>
    /// <param name="claimChecker">The optional checker adapter.</param>
    public PublicationService(
        IClaimCheckingService claimCheckingService,
        IEvidenceFrontierService evidenceFrontierService,
        ClaimCheckingOptions claimCheckingOptions,
        ILogger<PublicationService> logger,
        IClaimChecker? claimChecker = null)
    {
        this._claimCheckingService = claimCheckingService ?? throw new ArgumentNullException(nameof(claimCheckingService));
        this._evidenceFrontierService = evidenceFrontierService ?? throw new ArgumentNullException(nameof(evidenceFrontierService));
        this._claimCheckingOptions = claimCheckingOptions ?? throw new ArgumentNullException(nameof(claimCheckingOptions));
        this._logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this._claimChecker = claimChecker;
    }

    /// <inheritdoc />
    public async Task<Result<PublicationOutcome>> PublishAsync(PublicationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var shouldCheck = this._claimCheckingOptions.Enabled && this._claimChecker is not null && request.Validation.Draft.Tracks.Count > 0;
        MentalModel model;
        ClaimCheckingSummary? checkingSummary = null;
        var checkerFailed = false;
        if (shouldCheck)
        {
            var checking = await this._claimCheckingService.CheckAsync(request.Validation, request.Binder, cancellationToken);
            if (checking.IsFailure)
            {
                return Result.ErrorFromResult<PublicationOutcome>(checking);
            }

            model = checking.Data!.Model;
            checkingSummary = checking.Data.Summary;
            checkerFailed = checkingSummary.ParseFailure is not null;
        }
        else
        {
            model = MentalModelFactory.FromDraft(request.Validation.Draft);
        }

        var repaired = ShapeRepairer.Repair(model);
        var facts = new CheckerFacts(shouldCheck, checkerFailed);
        var citations = CitationBuilder.Build(repaired.Model, request.Binder, request.Graph, facts);
        var readingModel = ReadingModelBuilder.Build(request.Binder.Comparison, repaired.Model, citations, request.Binder,
            request.Graph, facts);
        var usedNodeIds = shouldCheck
            ? readingModel.Citations.Where(citation => citation.Provenance != CitationProvenance.Unchecked)
                .Select(citation => citation.NodeId)
                .ToHashSet(StringComparer.Ordinal)
            : null;
        var frontier = this._evidenceFrontierService.Build(request.Ranking, request.Graph, request.Policy, request.Binder,
            usedNodeIds, cancellationToken);
        var outcome = new PublicationOutcome(readingModel, frontier, repaired.Repairs, checkingSummary);
        this._logger.LogInformation(
            "Published reading model for run {RunId}: {TrackCount} tracks, {CitationCount} citations, " +
            "{RepairCount} shape repairs, checker ran {CheckerRan}, frontier {FrontierReturned}/{FrontierTotal}.",
            request.Binder.Comparison.RunId,
            repaired.Model.Tracks.Count,
            citations.Count,
            repaired.Repairs.Count,
            shouldCheck,
            frontier.Diagnostics.EntryCount,
            frontier.Diagnostics.TotalEntryCount);
        return Result.Success(outcome);
    }
}
